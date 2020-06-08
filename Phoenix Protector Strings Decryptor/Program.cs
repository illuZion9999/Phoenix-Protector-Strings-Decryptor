using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Mime;
using Colorful;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using Console = Colorful.Console;

namespace Phoenix_Protector_Strings_Decryptor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Phoenix Protector Strings Decryptor by illuZion";
            Console.WriteAscii("Phoenix Protector", Color.Red);
            Console.WriteAscii("Strings Decryptor", Color.Orange);
            Console.WriteLine("v1.0 by illuZion ", Color.Blue);

            var targetFilePath = string.Empty;
            if (args.Length < 1)
            {
                while (targetFilePath == string.Empty || !File.Exists(targetFilePath))
                {
                    Console.Write("Path of the file: ");
                    targetFilePath = Path.GetFullPath(Console.ReadLine().Replace("\"", string.Empty));
                }
            }
            else
                targetFilePath = Path.GetFullPath(args[0]);
            
            ModuleDefMD module = null;
            try
            {
                module = ModuleDefMD.Load(targetFilePath);
            }
            catch (Exception ex)
            {
                throw new BadImageFormatException("Module couldn't have been loaded.", ex);
            }
            if (module == null)
                throw new BadImageFormatException("Module couldn't have been loaded.");
            
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            int decryptedCount = StringsDecryptor.DecryptStrings(module);
            
            stopwatch.Stop();
            Console.WriteLine($"\n[+] Decrypted {decryptedCount} strings ! Elapsed time: {stopwatch.Elapsed.TotalSeconds}", Color.Green);
            Console.WriteLine("[$] Saving the file...", Color.Aqua);

            string outputFilePath = $"{Path.GetDirectoryName(targetFilePath)}\\{Path.GetFileNameWithoutExtension(targetFilePath)}-decrypted{Path.GetExtension(targetFilePath)}";
            ModuleWriterOptionsBase moduleWriterOptionsBase = module.IsILOnly
                ? new ModuleWriterOptions(module)
                : (ModuleWriterOptionsBase) new NativeModuleWriterOptions(module, true);
            
            moduleWriterOptionsBase.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
            // Prevents dnlib from throwing non-important errors.
            moduleWriterOptionsBase.Logger = DummyLogger.NoThrowInstance;
            moduleWriterOptionsBase.MetadataLogger = DummyLogger.NoThrowInstance;

            // Saves the output (unpacked) module.
            if (moduleWriterOptionsBase is NativeModuleWriterOptions nativeModuleWriterOptions)
                module.NativeWrite(outputFilePath, nativeModuleWriterOptions);
            else
                module.Write(outputFilePath, moduleWriterOptionsBase as ModuleWriterOptions);
            
            Console.WriteLine($"[+] File saved at {outputFilePath}", Color.Green);
            
            Console.ReadKey();
            Environment.Exit(0);
        }
    }
}