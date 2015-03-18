﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.IO;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.Xml;
using Microsoft.Research.ReviewBot;
using System.Reflection;

namespace Microsoft.Research.ReviewBot.Github
{
  class Usable 
  {
    public string SolutionPath;
    public string ProjectPath;
  }
  class Program
  {
    //static readonly string gitCmd = @"C:\Program Files (x86)\Git\bin\git.exe";
    static readonly string gitCmd = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)"), "git", "bin", "git.exe");
    static readonly string selectedReposPath = @"..\..\selectedRepos.txt";
    static readonly string selectedSolutionsPath = @"..\..\selectedSolutions.txt";
    static readonly string scanResultsPath = @"..\..\scanresults.json";
    static readonly string msbuildResultsPath = @"..\..\msbuildresults.txt";
    static readonly string msbuildPath = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)"), "MSBuild", "14.0", "Bin", "msbuild.exe");
    //static readonly string msbuildPath = "msbuild.exe";
    static void Main(string[] args)
    {

      var githubResp = GetGitHubList(30);

      //CloneAll(githubResp);

      //tryBuildingAllWithRoslyn(githubResp);

      crossCheckWithMsbuild();
      var usables = findUsableProjects();
      Console.WriteLine(usables.Count);
      Console.WriteLine(" out of ");
      Console.WriteLine(countTotalProjects());
      //makeNicerResultsFile(results);
      //runReviewBotOnSelectedRepos();
      Console.WriteLine("press a key to exit");
      Console.ReadKey();
    }

    static int countTotalProjects()
    {
      int i = 0;
      var text = File.ReadAllText(scanResultsPath);
      var json = JsonConvert.DeserializeObject<ScanResult>(text);
      foreach (var result in json.results)
      {
        foreach (var sln in result.SolutionCanBuildPairs) {
          foreach (var p in sln.Projects)
          {
            ++i;
          }
        }
      }
      return i;
    }

    static List<Usable> findUsableProjects()
    {
      var usables = new List<Usable>();
      var text = File.ReadAllText(scanResultsPath);
      var json = JsonConvert.DeserializeObject<ScanResult>(text);
      foreach (var result in json.results)
      {
        foreach (var sln in result.SolutionCanBuildPairs)
        {
          if (sln.canMsBuild)
          {
            foreach (var p in sln.Projects)
            {
              if (p.canRoslynOpen)
              {
                var usable = new Usable();
                usable.SolutionPath = sln.FilePath;
                usable.ProjectPath = p.FilePath;
                usables.Add(usable);
                //Console.WriteLine(p.FilePath);
              }
            }
          }
        }
      }
      return usables;
    }

    static void CloneAll(SearchResponse resp)
    {
      foreach (var result in resp.items)
      {
        CloneRepo(result.clone_url);
      }
    }

    static SearchResponse GetGitHubList(int nRepos)
    {
      int nPages =1;
      var urlWithHole = "https://api.github.com/search/repositories?q=language:csharp&sort=stars&order=desc&page={0}&per_page={1}/";
      var url = String.Format(urlWithHole, nPages, nRepos);
      var request = HttpWebRequest.Create(new Uri(url)) as HttpWebRequest;
      request.UserAgent = "ReviewBot"; // YOU MUST HAVE THIS SET TO SOMETHING !!!!
      var resp = request.GetResponse() as HttpWebResponse;
      var reader = new StreamReader(resp.GetResponseStream());
      return JsonConvert.DeserializeObject<SearchResponse>(reader.ReadToEnd());
    }

    static void writeJson(ScanResult sr, string path)
    {
      var text = JsonConvert.SerializeObject(sr, Newtonsoft.Json.Formatting.Indented);
      File.WriteAllText(scanResultsPath, text);
    }

    static void CloneRepo(string url)
    {
      Process p = new Process();
      p.StartInfo.FileName = gitCmd;
      p.StartInfo.Arguments = String.Format("clone {0}", url);
      p.StartInfo.UseShellExecute = true;
      p.Start();
      p.WaitForExit();
    }
    static bool Msbuild(string slnPath)
    {
      var p = new Process();
      p.StartInfo.FileName = msbuildPath;
      p.StartInfo.Arguments = slnPath + " /toolsversion:12.0"; // I needed the tools version.  maybe you dont
      //p.StartInfo.WorkingDirectory = Directory.GetParent(slnPath).ToString();
      p.StartInfo.UseShellExecute = false;
      p.Start();
      p.WaitForExit();
      return p.ExitCode == 0;

    }
    static void crossCheckWithMsbuild()
    {
      var hits = new List<string>();
      var misses = new List<string>();
      var text = File.ReadAllText(scanResultsPath);
      var json = JsonConvert.DeserializeObject<ScanResult>(text);
      var res = File.OpenWrite(msbuildResultsPath);
      foreach (var result in json.results)
      {
        foreach(var sln in result.SolutionCanBuildPairs)
        {
          sln.canMsBuild = Msbuild(sln.FilePath);
          //if (Msbuild(sln.FilePath))
          //{
          //  hits.Add(sln.FilePath);
          //}
          //else
          //{
          //  misses.Add(sln.FilePath);
          //}
        }
      }
      //Console.WriteLine("MSBuild couldn't build:");
      //misses.ForEach(Console.WriteLine);
      //Console.WriteLine("MSBuild built:");
      //hits.ForEach(Console.WriteLine);
      //File.WriteAllLines(selectedSolutionsPath, hits);
      writeJson(json, scanResultsPath);
    }
    static void makeNicerResultsFile(SearchResponse rsp)
    {
      var selections = File.ReadAllLines(@"..\..\selectedSolutions.txt");
      var data = new List<RepoData>();
      foreach (var hit in selections)
      {
        var hitName = hit.Remove(hit.IndexOf("\\"));
        var githubInfo = rsp.items.First(x => x.name == hitName);
        var rd = new RepoData();
        rd.cloneUrl = githubInfo.clone_url;
        rd.name = hitName;
        rd.selectedSolutionPath = hit;
        data.Add(rd);
      }
      var text = JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
      File.WriteAllText(selectedReposPath, text);
      Console.WriteLine(text);

    }
    static void tryBuildingAllWithRoslyn(SearchResponse resp)
    {
      var sr = new ScanResult();
      foreach (var pair in resp.items)
      {
        var name = pair.name;
        var url = pair.clone_url;

        var slnPaths = SolutionTools.ScanForSolutions(name);
        var localResults = new RepoResult();
        localResults.RepoName = name;
        if (name.StartsWith("corefx"))
        { 
          localResults.skipped = true;
          localResults.comment = "project Microsoft.CSharp doesn't build with roslyn";
          sr.results.Add(localResults);
          continue;
        }
        if (name.StartsWith("mono"))
        { 
          localResults.skipped = true;
          localResults.comment = "mono takes forever to even open in roslyn";
          sr.results.Add(localResults);
          continue;
        }
        if (name.StartsWith("OpenRA"))
        { 
          localResults.skipped = true;
          localResults.comment = "openRA takes forever to open in roslyn";
          sr.results.Add(localResults);
          continue;
        }
        if (name.StartsWith("Newtonsoft.Json"))
        { 
          localResults.skipped = true;
          localResults.comment = "roslyn throws assert";
          sr.results.Add(localResults);
          continue;
        }
        if (name.StartsWith("roslyn"))
        { 
          localResults.skipped = true;
          localResults.comment = "roslyn throws assert";
          sr.results.Add(localResults);
          continue;
        }
        if (name.StartsWith("ravendb"))
        { 
          localResults.skipped = true;
          localResults.comment = "doesn't build with roslyn";
          sr.results.Add(localResults);
          continue;
        }
        if (name.StartsWith("SignalR"))
        { 
          localResults.skipped = true;
          localResults.comment = "roslyn cant open the solution";
          sr.results.Add(localResults);
          continue;
        }
        /*
        if (name.StartsWith("ILSpy"))
        { 
          localResults.skipped = true;
          localResults.comment = "doesn't build with roslyn";
          sr.results.Add(localResults);
          continue;
        }
        */
        foreach (var slnPath in slnPaths)
        {
          Console.WriteLine("In solution: " + slnPath);
          var msbw = MSBuildWorkspace.Create();
          var stats = new SolutionStats();
          stats.FilePath = slnPath;
          Solution sln = null;
          try
          {
            sln = msbw.OpenSolutionAsync(slnPath).Result;
            stats.canRoslynOpen = true;
            if (!sln.Projects.Any()) { throw new FileNotFoundException("no projects"); }
          }
          catch
          {
            stats.canRoslynOpen = false;
            continue;
          }
          var depGraph = sln.GetProjectDependencyGraph();
          var projs = depGraph.GetTopologicallySortedProjects();
          //var assemblies = new List<Stream>();
          //var results = new List<bool>();
          foreach (var projId in projs)
          {
            var pstats = new ProjectStats();
            pstats.canRoslynOpen = true;
            var proj = sln.GetProject(projId);
            pstats.FilePath = proj.FilePath;
            Console.WriteLine("Building project: " + proj.Name);
            var stream = new MemoryStream();
            try
            {
              var comp = proj.GetCompilationAsync().Result;
              string pathToDll = @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5.2\Facades\";
              var metadataReferences = new List<string> {
              "System.Runtime.dll",
              "System.Runtime.Extensions.dll",
              "System.Threading.Tasks.dll",
              "System.IO.dll",
              "System.Reflection.dll",
              "System.Text.Encoding.dll"}.Select(s => MetadataReference.CreateFromFile(pathToDll + s));
              comp = comp.AddReferences(metadataReferences);
              comp.AddReferences(new MetadataReference[] { MetadataReference.CreateFromAssembly(typeof(object).Assembly) });
              var errors = comp.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error);
              if (errors.Any())
              {
                pstats.Diagnostics = errors.Select(x => x.ToString()).ToList();
                pstats.canRoslynOpen = false;
              }
              //var result = proj.GetCompilationAsync().Result.Emit(stream);
              //results.Add(result.Success);
              //if (!result.Success)
              //{
              //  stats.Diagnostics = result.Diagnostics.Select(x => x.ToString()).ToList();
              //}
              //assemblies.Add(stream);
            }
            catch (Exception e)
            {
              var l = new List<string>();
              l.Add(e.Message);
              l.Add(e.StackTrace);
              pstats.Diagnostics.AddRange(l);
              pstats.canRoslynOpen = false;
            }
            stats.Projects.Add(pstats);
            //stats.canRoslynOpen = results.All(x => x);
          }
          localResults.SolutionCanBuildPairs.Add(stats);
        }
        sr.results.Add(localResults);
      }
      writeJson(sr, scanResultsPath);
      
    }
    static void runReviewBotOnSelectedRepos()
    {
      var text = File.ReadAllText(selectedReposPath);
      var repos = JsonConvert.DeserializeObject<List<RepoData>>(text);
      foreach (var repo in repos)
      {
        CloneRepo(repo.cloneUrl);

        // copy our props file to the solution dir
        try
        {

          File.Copy(@"..\..\Common.CodeContracts.props", Path.Combine(Path.GetDirectoryName(repo.selectedSolutionPath), "Common.CodeContracts.props"));
        }
        catch (IOException)
        { }

        // rewrite every project
        MSBuildWorkspace msbw = null;
        try
        {
          msbw = MSBuildWorkspace.Create();

        }
        catch (ReflectionTypeLoadException ex)
        {
          StringBuilder sb = new StringBuilder();
          foreach (Exception exSub in ex.LoaderExceptions)
          {
            sb.AppendLine(exSub.Message);
            FileNotFoundException exFileNotFound = exSub as FileNotFoundException;
            if (exFileNotFound != null)
            {
              if (!string.IsNullOrEmpty(exFileNotFound.FusionLog))
              {
                sb.AppendLine("Fusion Log:");
                sb.AppendLine(exFileNotFound.FusionLog);
              }
            }
            sb.AppendLine();
          }
          string errorMessage = sb.ToString();
          //Display or log the error based on your application.
          Console.WriteLine(errorMessage);
        }
        var sln = msbw.OpenSolutionAsync(repo.selectedSolutionPath).Result;
        foreach (var proj in sln.Projects)
        {
          RewriteProject(proj.FilePath);
        }
        Msbuild(repo.selectedSolutionPath);

        var rsps = Directory.GetFiles(Path.GetDirectoryName(repo.selectedSolutionPath), "*.rsp", SearchOption.AllDirectories);

        // run reviewbot
        foreach(var proj in sln.Projects)
        {
          var conf = new Configuration();
          var defaultConfig = Configuration.GetDefaultConfiguration();
          conf.GitRoot = Path.Combine(Directory.GetCurrentDirectory(), repo.name);
          conf.Git = gitCmd;
          conf.MSBuild = msbuildPath;
          conf.Solution = repo.selectedSolutionPath;
          conf.Cccheck = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)"), "Microsoft", "Contracts", "bin", "cccheck.exe");
          conf.Project = proj.FilePath;
          conf.CccheckOptions = "-xml -remote=false -suggest methodensures -suggest propertyensures -suggest objectinvariants -suggest necessaryensures  -suggest readonlyfields -suggest assumes -suggest nonnullreturn -sortWarns=false -warninglevel full -maxwarnings 99999999";

          // if the next line dies, it couldnt find the rsp.  do you have cccheck installed?
          conf.RSP = Path.Combine(Directory.GetCurrentDirectory(), rsps.First(x => x.Contains(proj.Name + "cccheck.rsp")));
          conf.GitBaseBranch = "master";
          conf.CccheckXml = Path.Combine(Directory.GetCurrentDirectory(), repo.name + "_" + proj.Name + "_clousot.xml");

          ReviewBotMain.BuildAnalyzeInstrument(conf);

          //if (!ExternalCommands.TryRunClousot(conf.CccheckXml, conf.Cccheck, conf.CccheckOptions, conf.RSP))
          //{
          //  Console.WriteLine("couldn't run clousot");
          //  return;
          //}
          //return;

        }
      }
    }
    static void RewriteProject(string path)
    {
      var oldDoc = new XmlDocument();
      oldDoc.LoadXml(File.ReadAllText(path));
      var children = oldDoc.ChildNodes;
      foreach (var child in children)
      {
        var node = child as XmlElement;
        if (node != null )
        {
          var newnode = oldDoc.CreateElement("Import", oldDoc.DocumentElement.NamespaceURI);
          newnode.SetAttribute("Project", "$(SolutionDir)\\Common.CodeContracts.props");
          node.AppendChild(newnode);

        }
      }
      oldDoc.Save(path);
    }
  }
} 