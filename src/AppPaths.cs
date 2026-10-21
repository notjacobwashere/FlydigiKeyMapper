namespace FlydigiKeyMapper;
static class AppPaths {
 public static bool TestMode {get;set;}
 static readonly string testFolder=Path.Combine(Path.GetTempPath(),"FlydigiKeyMapper-tests",Guid.NewGuid().ToString("N"));
 public static string DataDirectory {get{string path=TestMode?testFolder:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlydigiKeyMapper");Directory.CreateDirectory(path);return path;}}
 public static string FilePath(string name)=>Path.Combine(DataDirectory,name);
 public static string Configuration {get{string destination=FilePath("settings.json"),legacy=Path.Combine(AppContext.BaseDirectory,"settings.json");if(!TestMode&&!File.Exists(destination)&&File.Exists(legacy))File.Copy(legacy,destination);return destination;}}
}
