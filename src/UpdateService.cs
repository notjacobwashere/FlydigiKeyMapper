using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
namespace FlydigiKeyMapper;
record ReleaseInfo(Version Version,string Tag,string Notes,Uri Page,Uri? Installer);
static class UpdateService {
 public const string Repository="notjacobwashere/FlydigiKeyMapper";
 public const string RepositoryUrl="https://github.com/"+Repository;
 public static Version Current {get{var v=Assembly.GetExecutingAssembly().GetName().Version??new Version(0,1,0);return new Version(v.Major,v.Minor,Math.Max(0,v.Build));}}
 public static Version? ParseVersion(string tag)=>Version.TryParse(tag.TrimStart('v','V'),out var version)?version:null;
 public static Uri? TrustedPage(string? url){if(Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)&&uri.AbsolutePath.StartsWith("/"+Repository+"/releases/",StringComparison.Ordinal))return uri;return null;}
 public static ReleaseInfo Parse(string json){using var doc=JsonDocument.Parse(json);var root=doc.RootElement;if(root.GetProperty("draft").GetBoolean()||root.GetProperty("prerelease").GetBoolean())throw new InvalidDataException("The release is not a stable public release.");string tag=root.GetProperty("tag_name").GetString()??"";var version=ParseVersion(tag)??throw new InvalidDataException("Unrecognized release version.");var page=TrustedPage(root.GetProperty("html_url").GetString())??throw new InvalidDataException("Unexpected release URL.");Uri? installer=null;
 foreach(var asset in root.GetProperty("assets").EnumerateArray())if(asset.GetProperty("name").GetString()==$"FlydigiKeyMapper-Setup-{version.ToString(3)}-x64.exe")installer=TrustedPage(asset.GetProperty("browser_download_url").GetString());
 return new(version,tag,root.TryGetProperty("body",out var body)?body.GetString()??"":"",page,installer);}
 public static async Task<ReleaseInfo?> Check(CancellationToken cancel){using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(20)};http.DefaultRequestHeaders.UserAgent.ParseAdd("FlydigiKeyMapper/"+Current.ToString(3));http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");using var response=await http.GetAsync("https://api.github.com/repos/"+Repository+"/releases/latest",cancel);if(response.StatusCode==HttpStatusCode.NotFound)return null;if(response.StatusCode==HttpStatusCode.Forbidden)throw new IOException("GitHub's request limit was reached. Try again later.");response.EnsureSuccessStatusCode();return Parse(await response.Content.ReadAsStringAsync(cancel));}
}

