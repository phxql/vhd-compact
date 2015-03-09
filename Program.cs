using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VhdCompact
{
    class Program
    {
        private enum ExitCode: int
        {
            WrongArguments = 1,
            DiskpartFailed = 2,
            AttachFailed = 3,
            DefragFailed = 4,
            VhdFileNotFound = 5
        }

        static void Main(string[] args)
        {
            new Program().Run(args);
        }

        private void Run(string[] args)
        {
            if (args.Length != 1)
            {
                PrintHelp();
                Environment.Exit((int)ExitCode.WrongArguments);
            }

            var vhdFile = Path.GetFullPath(args[0]);
            if (!File.Exists(vhdFile))
            {
                Fail(ExitCode.VhdFileNotFound, "VHD file {0} doesn't exists", vhdFile);
                return;
            }
            Console.WriteLine("VHD file: " + vhdFile);
            
            var drive = GetFreeDriveLetter();
            Console.WriteLine("Temporary drive letter " + drive);
            
            // Attach
            var result = RunDiskPart("select vdisk file=\"{0}\"".F(vhdFile), "attach vdisk");
            CheckResult(result);
            
            // Wait a little bit
            Thread.Sleep(TimeSpan.FromSeconds(1));

            // TODO: Handle multi-partition VHDs!
            // Assign drive letter
            result = RunDiskPart("select vdisk file=\"{0}\"".F(vhdFile), "select part 1", "assign letter={0}".F(drive));
            CheckResult(result);

            if (!DriveLetterExists(drive))
            {
                Fail(ExitCode.AttachFailed, "Attaching the drive failed. Maybe the drive is a GPT drive? Currently only MBR drives are supported.");
                return;
            }

            // Defrag volume
            var exitCode = RunDefrag(drive);
            CheckDefragResult(exitCode);

            // Detach
            result = RunDiskPart("select vdisk file=\"{0}\"".F(vhdFile), "detach vdisk");
            CheckResult(result);

            // Attach as readonly and compact
            result = RunDiskPart("select vdisk file=\"{0}\"".F(vhdFile), "attach vdisk readonly", "compact vdisk");
            CheckResult(result);

            // Detach
            result = RunDiskPart("select vdisk file=\"{0}\"".F(vhdFile), "detach vdisk");
            CheckResult(result);

            Success();
        }

        private void Fail(ExitCode exitCode, string reason, params object[] args)
        {
            Console.Error.WriteLine(reason, args);
            Console.ReadKey();            

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");

            Environment.Exit((int)exitCode);
        }

        private void Success()
        {
            Console.WriteLine("Compacting done. Press any key to exit...");
            Console.ReadKey();            
        }

        private void CheckDefragResult(int exitCode)
        {
            if (exitCode != 0)
            {
                Fail(ExitCode.DefragFailed, "ERROR: Expected return code 0, got {0}.",exitCode);
            }
        }

        private int RunDefrag(char drive)
        {
            var defrag = Path.Combine(Environment.SystemDirectory, "Defrag.exe");
            var processStartInfo = new ProcessStartInfo
            {
                FileName = defrag,
                Arguments = "{0}: /U /V /X".F(drive)
            };
            var process = Process.Start(processStartInfo);
            if (process == null)
            {
                throw new Exception("Process has been reused, this shouldn't have happend");
            }
            process.WaitForExit();

            return process.ExitCode;
        }

        private void CheckResult(Tuple<int, string> result)
        {
            if (result.Item1 != 0)
            {
                Fail(ExitCode.DiskpartFailed, "ERROR: Expected return code 0, got {0}. Output: {1}", result.Item1, result.Item2);
            }
        }

        private Tuple<int, string> RunDiskPart(string command, params string[] commands)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "diskpart.exe",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var process = Process.Start(processStartInfo);
            if (process == null)
            {
                throw new Exception("Process has been reused, this shouldn't have happend");
            }
            process.StandardInput.WriteLine(command);
            foreach (var c in commands)
            {
                process.StandardInput.WriteLine(c);
            }

            process.StandardInput.WriteLine("exit");
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return new Tuple<int, string>(process.ExitCode, output);
        }

        private void PrintHelp()
        {
            Console.WriteLine("Usage: VhdCompact.exe [Path to VHD file]");
        }

        /// <summary>
        /// Checks if the drive with the given letter exists.
        /// </summary>
        /// <param name="letter">Letter.</param>
        /// <returns><c>True</c> if the drive exists, <c>false</c> otherwise.</returns>
        private static bool DriveLetterExists(char letter)
        {
            var letters = new HashSet<char>();
            var drives = Directory.GetLogicalDrives();
            foreach (var drive in drives)
            {
                letters.Add(drive[0]);
            }

            return letters.Contains(letter);
        }

        /// <summary>
        /// Finds a free drive letter.
        /// </summary>
        /// <returns>Free drive letter.</returns>
        private static char GetFreeDriveLetter()
        {
            var availableLetters = new HashSet<char>();
            foreach (var c in "DEFGHIJKLMOPQRSTUVWXYZ")
            {
                availableLetters.Add(c);
            }

            var drives = Directory.GetLogicalDrives();
            foreach (var drive in drives)
            {
                availableLetters.Remove(drive[0]);
            }

            var enumerator = availableLetters.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new Exception("No free drive letter found");
            }
            return enumerator.Current;
        }
    }
}
