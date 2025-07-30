using System;
using System.Diagnostics;
using log4net;
using System.Reflection;
using System.IO;
using System.Threading;

namespace OWASP.WebGoat.NET.App_Code
{
    public class Util
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public static int RunProcessWithInput(string cmd, string args, string input)
        {
            // Validate executable and arguments before use
            if (!IsSafeExecutable(cmd))
            {
                log.Error("Unsafe executable name: " + cmd);
                throw new ArgumentException("Unsafe executable name.");
            }
            if (!IsSafeFilePath(args))
            {
                log.Error("Unsafe file path in arguments: " + args);
                throw new ArgumentException("Unsafe file path in arguments.");
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                WorkingDirectory = Settings.RootDir,
                FileName = cmd,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            using (Process process = new Process())
            {
                process.EnableRaisingEvents = true;
                process.StartInfo = startInfo;

                process.OutputDataReceived += (sender, e) => {
                    if (e.Data != null)
                        log.Info(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        log.Error(e.Data);
                };

                AutoResetEvent are = new AutoResetEvent(false);

                process.Exited += (sender, e) => 
                {
                    Thread.Sleep(1000);
                    are.Set();
                    log.Info("Process exited");

                };

                process.Start();

                using (StreamReader reader = new StreamReader(new FileStream(input, FileMode.Open)))
                {
                    string line;
                    string replaced;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                            replaced = line.Replace("DB_Scripts/datafiles/", "DB_Scripts\\\\datafiles\\\\");
                        else
                            replaced = line;

                        log.Debug("Line: " + replaced);

                        process.StandardInput.WriteLine(replaced);
                    }
                }
    
                process.StandardInput.Close();
    

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
    
                //NOTE: Looks like we have a mono bug: https://bugzilla.xamarin.com/show_bug.cgi?id=6291
                //have a wait time for now.
                
                are.WaitOne(10 * 1000);

                if (process.HasExited)
                    return process.ExitCode;
                else //WTF? Should have exited dammit!
                {
                    process.Kill();
                    return 1;
                }
            }
        }
        // Helper method to validate executable name/path
        private static bool IsSafeExecutable(string exe)
        {
            // Only allow known safe executables (e.g., sqlite3)
            string[] allowedExecutables = { "sqlite3", "sqlite3.exe" };
            string exeName = Path.GetFileName(exe).ToLowerInvariant();
            foreach (var allowed in allowedExecutables)
            {
                if (exeName == allowed)
                    return true;
            }
            return false;
        }

        // Helper method to validate file path arguments
        private static bool IsSafeFilePath(string path)
        {
            // Only allow file paths with safe characters
            // Disallow command line metacharacters
            char[] unsafeChars = { ';', '&', '|', '>', '<', '`', '$', '%', '!', '=', '\'', '"' };
            foreach (var c in unsafeChars)
            {
                if (path.Contains(c.ToString()))
                    return false;
            }
            // Optionally, check for absolute/relative path and extension
            // Only allow .db or .sqlite files
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".db" && ext != ".sqlite")
                return false;
            return true;
        }
    }
}
