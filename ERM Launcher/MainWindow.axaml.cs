using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Handlers;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Newtonsoft.Json;

namespace ERM_Launcher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Focus();
    }

    public static long expectedBytes;
    public static DateTime lastPull = DateTime.Now;
    
    private async void StyledElement_OnInitialized(object? sender, EventArgs e)
    {
        HttpClientHandler handler = new HttpClientHandler() { AllowAutoRedirect = true };
        ProgressMessageHandler progress = new ProgressMessageHandler(handler);

        progress.HttpReceiveProgress += (_, args) =>
        {
            double progressPercentage = args.ProgressPercentage;
            
            if((int)progressPercentage == (int)ProgressIndicator.Value || lastPull.Add(TimeSpan.FromMilliseconds(100)) > DateTime.Now)
            {
                return;
            }

            lastPull = DateTime.Now;
            string totalMb = ((double)expectedBytes / 1024 / 1024).ToString("0.0") + "MB";
            string receivedMb = ((double)args.BytesTransferred / 1024 / 1024).ToString("0.0") + "MB";
            
            Dispatcher.UIThread.Post(() =>
            {
                ProgressIndicator.Value = progressPercentage;
                StatusIndicator.Text = $"Downloading {progressPercentage}% - {receivedMb}/{totalMb}...";
            });
        };
        
        HttpClient fetchClient = new HttpClient(handler);
        fetchClient.DefaultRequestHeaders.Add("User-Agent", "request");
        
        HttpClient downloadClient = new HttpClient(progress);
        fetchClient.DefaultRequestHeaders.Add("User-Agent", "request");
        
        string filePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string ermDir = Path.Join(filePath, "ERM");

        DateTime lastChanged = DateTimeOffset.FromUnixTimeSeconds(0).DateTime;
        
        if(!Directory.Exists(ermDir))
        {
            Directory.CreateDirectory(ermDir);
        }

        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if(File.Exists(Path.Join(ermDir, "ERM.Desktop.exe")))
            {
                lastChanged = File.GetLastWriteTimeUtc(Path.Join(ermDir, "ERM.Desktop.exe"));
            }
        }
        else if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if(File.Exists(Path.Join(ermDir, "version.txt")))
            {
                lastChanged = DateTimeOffset
                    .FromUnixTimeMilliseconds(long.Parse(await File.ReadAllTextAsync(Path.Join(ermDir, "version.txt")))).DateTime;
            }
        }

        HttpResponseMessage fetchResponse = await fetchClient.GetAsync("https://api.github.com/repos/1FriendlyDoge/Desktop-Releases/releases/latest");

        
        if(fetchResponse.StatusCode == HttpStatusCode.TooManyRequests)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusIndicator.Text = "GitHub rate limit exceeded. Launching anyways.";
            });
            
            LaunchApp(ermDir);

            await Task.Delay(3000);
            Environment.Exit(0);
        }
        else if(!fetchResponse.IsSuccessStatusCode)
        {
            Environment.Exit(1);
        }
        
        GithubRelease? githubRelease = JsonConvert.DeserializeObject<GithubRelease>(await fetchResponse.Content.ReadAsStringAsync());
        
        if(githubRelease == null)
        {
            Environment.Exit(1);
        }
        
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Asset? asset = githubRelease.assets.Find(x => x.name == "ERM.Desktop.exe");
            
            if(asset != null && lastChanged < githubRelease.published_at)
            {
                expectedBytes = asset.size;
                
                HttpResponseMessage downloadResponse = await downloadClient.GetAsync(asset.browser_download_url);
                Task<byte[]> receiveTask = downloadResponse.Content.ReadAsByteArrayAsync();
                byte[] data = await receiveTask;
                    
                if(receiveTask.IsCompletedSuccessfully)
                {
                    try
                    {
                        await File.WriteAllBytesAsync(Path.Join(ermDir, "ERM.Desktop.exe"), data);
                    }
                    catch(Exception)
                    {
                        // ignored
                    }
                    
                }
            }
        }
        else if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Asset? asset = githubRelease.assets.Find(x => x.name == "ERM.Desktop.MacOS.zip");
            
            if(asset != null && lastChanged < githubRelease.published_at)
            {
                expectedBytes = asset.size;
                
                HttpResponseMessage downloadResponse = await downloadClient.GetAsync(asset.browser_download_url);
                Task<byte[]> receiveTask = downloadResponse.Content.ReadAsByteArrayAsync();
                byte[] data = await receiveTask;
                    
                if(receiveTask.IsCompletedSuccessfully)
                {
                    try
                    {
                        await File.WriteAllBytesAsync(Path.Join(ermDir, "ERM.Desktop.MacOS.zip"), data);
                    }
                    catch(Exception)
                    {
                        // ignored
                    }

                    Dispatcher.UIThread.Post(() =>
                    {
                        StatusIndicator.Text = "Extracting...";
                    });

                    await Task.Run(() =>
                    {
                        System.IO.Compression.ZipFile.ExtractToDirectory(Path.Join(ermDir, "ERM.Desktop.MacOS.zip"), ermDir, true);
                    });
                    
                    try
                    {
                        await File.WriteAllTextAsync(Path.Join(ermDir, "version.txt"), DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                    
                    if(Directory.Exists(Path.Join(ermDir, "ERM Desktop.app")))
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo("chmod", $"+x \"{Path.Join(ermDir, "ERM Desktop.app", "Contents", "MacOS", "ERM Desktop")}\"");

                        Process process = new Process();
                        process.StartInfo = startInfo;
                        process.Start();
                        await process.WaitForExitAsync();
                    }

                    File.Delete(Path.Join(ermDir, "ERM.Desktop.MacOS.zip"));
                }
            }
        }
        
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ProgressIndicator.Value = 100;
            StatusIndicator.Text = "Launching...";
        });
        
        LaunchApp(ermDir);
        
        await Task.Delay(3000);
        
        Environment.Exit(0);
    }
    
    public void LaunchApp(string parentDir)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if(File.Exists(Path.Join(parentDir, "ERM.Desktop.exe")))
            {
                Process.Start(new ProcessStartInfo(Path.Join(parentDir, "ERM.Desktop.exe")));
            }
        }
        else if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if(Directory.Exists(Path.Join(parentDir, "ERM Desktop.app")))
            {
                Process.Start(new ProcessStartInfo("open", $"-a \"{Path.Join(parentDir, "ERM Desktop.app")}\""));
            }
        }
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        Environment.Exit(0);
    }
}