using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
namespace FlydigiKeyMapper;
record ReleaseInfo(Version Version,string Tag,string Notes,Uri Page,Uri? Installer,string? Sha256=null,long Size=0);
static class UpdateService {
 public const string Repository="notjacobwashere/FlydigiKeyMapper";
 public const string RepositoryUrl="https://github.com/"+Repository;
 public static Version Current {get{var v=Assembly.GetExecutingAssembly().GetName().Version??new Version(0,1,0);return new Version(v.Major,v.Minor,Math.Max(0,v.Build));}}
 public static Version? ParseVersion(string tag)=>Version.TryParse(tag.TrimStart('v','V'),out var version)&&version.Build>=0?version:null;
 public static Uri? TrustedPage(string? url){if(Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo==""&&uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)&&uri.AbsolutePath.StartsWith("/"+Repository+"/releases/",StringComparison.Ordinal))return uri;return null;}
 public static ReleaseInfo Parse(string json){
  using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
  if(root.GetProperty("draft").GetBoolean()||root.GetProperty("prerelease").GetBoolean())throw new InvalidDataException("The release is not a stable public release.");
  string tag=root.GetProperty("tag_name").GetString()??"";var version=ParseVersion(tag)??throw new InvalidDataException("Unrecognized release version.");
  var page=TrustedPage(root.GetProperty("html_url").GetString())??throw new InvalidDataException("Unexpected release URL.");Uri? installer=null;string? hash=null;long size=0;
  string name=$"FlydigiKeyMapper-Setup-{version.ToString(3)}-x64.exe";
  foreach(var asset in root.GetProperty("assets").EnumerateArray())if(asset.GetProperty("name").GetString()==name){
   var candidate=TrustedPage(asset.GetProperty("browser_download_url").GetString());
   if(candidate?.AbsoluteUri!=$"{RepositoryUrl}/releases/download/{Uri.EscapeDataString(tag)}/{name}")continue;
   installer=candidate;
   string digest=asset.TryGetProperty("digest",out var d)?d.GetString()??"":"";
   if(digest.StartsWith("sha256:",StringComparison.Ordinal)&&digest.Length==71&&digest[7..].All(Uri.IsHexDigit))hash=digest[7..];
   if(asset.TryGetProperty("size",out var s))size=s.GetInt64();
  }
  return new(version,tag,root.TryGetProperty("body",out var body)?body.GetString()??"":"",page,installer,hash,size);
 }
 static HttpClient Client(TimeSpan timeout){var http=new HttpClient{Timeout=timeout};http.DefaultRequestHeaders.UserAgent.ParseAdd("FlydigiKeyMapper/"+Current.ToString(3));return http;}
 public static async Task<ReleaseInfo?> Check(CancellationToken cancel){using var http=Client(TimeSpan.FromSeconds(20));http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");using var response=await http.GetAsync("https://api.github.com/repos/"+Repository+"/releases/latest",cancel);if(response.StatusCode==HttpStatusCode.NotFound)return null;if(response.StatusCode==HttpStatusCode.Forbidden)throw new IOException("GitHub's request limit was reached. Try again later.");response.EnsureSuccessStatusCode();return Parse(await response.Content.ReadAsStringAsync(cancel));}
 public static async Task VerifyInstaller(string path,ReleaseInfo release,CancellationToken cancel){
  if(release.Sha256==null||release.Size<=0)throw new InvalidDataException("GitHub did not provide a SHA-256 checksum and size. The update cannot be installed automatically.");
  using var file=File.OpenRead(path);
  if(file.Length!=release.Size)throw new InvalidDataException("The installer download is incomplete.");
  string actual=Convert.ToHexString(await SHA256.HashDataAsync(file,cancel));
  if(!actual.Equals(release.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The installer checksum did not match GitHub. Nothing was installed.");
 }
 public static async Task<string> Download(ReleaseInfo release,string directory,IProgress<int> progress,CancellationToken cancel){
  if(release.Installer==null||release.Sha256==null||release.Size<=0||release.Size>512L*1024*1024)throw new InvalidDataException("This release does not have a verifiable installer.");
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel);timeout.CancelAfter(TimeSpan.FromMinutes(20));cancel=timeout.Token;
  Directory.CreateDirectory(directory);string path=Path.Combine(directory,$"FlydigiKeyMapper-Setup-{release.Version.ToString(3)}-x64.exe"),partial=path+".partial";
  try{
   using var http=Client(TimeSpan.FromMinutes(20));using var response=await http.GetAsync(release.Installer,HttpCompletionOption.ResponseHeadersRead,cancel);response.EnsureSuccessStatusCode();
   var final=response.RequestMessage?.RequestUri;
   if(final==null||final.Scheme!="https"||!(final.Host=="github.com"||final.Host=="release-assets.githubusercontent.com"||final.Host=="objects.githubusercontent.com"))throw new IOException("Unexpected installer download location.");
   await using(var source=await response.Content.ReadAsStreamAsync(cancel))
   await using(var file=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)){
    byte[] buffer=new byte[65536];long total=0;int count;
    while((count=await source.ReadAsync(buffer,cancel))!=0){total+=count;if(total>release.Size)throw new InvalidDataException("The download exceeded the expected size.");await file.WriteAsync(buffer.AsMemory(0,count),cancel);progress.Report((int)(total*100/release.Size));}
   }
   await VerifyInstaller(partial,release,cancel);File.Move(partial,path);return path;
  }catch{File.Delete(partial);throw;}
 }
}
