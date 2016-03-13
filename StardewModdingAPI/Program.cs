﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StardewModdingAPI.Events;
using StardewModdingAPI.ExtensionMethods;
using StardewModdingAPI.Helpers;
using StardewModdingAPI.Inheritance;
using StardewModdingAPI.Inheritance.Menus;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace StardewModdingAPI
{
    public class Program
    {
        private static List<string> _modPaths;
        private static List<string> _modContentPaths;

        public static Texture2D DebugPixel { get; private set; }

        public static bool IsGameReferenceDirty { get; set; }

        public static object gameInst;

        public static Game1 _gamePtr;
        public static Game1 gamePtr
        {
            get
            {
                if(IsGameReferenceDirty && gameInst != null)
                {
                    _gamePtr = gameInst.Copy<Game1>();
                }
                return _gamePtr;
            }
        }

        public static bool ready;

        public static Form StardewForm;

        public static Thread gameThread;
        public static Thread consoleInputThread;

        

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Main method holding the API execution
        /// </summary>
        /// <param name="args"></param>
        private static void Main(string[] args)
        {
            try
            {
                ConfigureUI();
                ConfigurePaths();
                ConfigureMethodInjection();
                ConfigureSDV();
                GameRunInvoker();
            }
            catch (Exception e)
            {
                // Catch and display all exceptions. 
                StardewModdingAPI.Log.Error("Critical error: " + e);
            }

            StardewModdingAPI.Log.Comment("The API will now terminate. Press any key to continue...");
            Console.ReadKey();
        }
    
        /// <summary>
        /// Configures Mono.Cecil injections
        /// </summary>
        private static void ConfigureMethodInjection()
        {
            StardewAssembly.ModifyStardewAssembly();

#if DEBUG
            StardewAssembly.WriteModifiedExe();
#endif
        }


        public static void Test(object instance)
        {
            gameInst = instance;
            IsGameReferenceDirty = true;
        }

        /// <summary>
        /// Set up the console properties
        /// </summary>
        private static void ConfigureUI()
        {
            Console.Title = Constants.ConsoleTitle;

#if DEBUG
            Console.Title += " - DEBUG IS NOT FALSE, AUTHOUR NEEDS TO REUPLOAD THIS VERSION";
#endif
        }

        /// <summary>
        /// Setup the required paths and logging
        /// </summary>
        private static void ConfigurePaths()
        {
            StardewModdingAPI.Log.Info("Validating api paths...");

            _modPaths = new List<string>();
            _modContentPaths = new List<string>();


            //TODO: Have an app.config and put the paths inside it so users can define locations to load mods from
            _modPaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "Mods"));
            _modPaths.Add(Path.Combine(Constants.ExecutionPath, "Mods"));
            _modContentPaths.Add(Path.Combine(Constants.ExecutionPath, "Mods", "Content"));
            _modContentPaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "Mods", "Content"));

            //Checks that all defined modpaths exist as directories
            _modPaths.ForEach(path => VerifyPath(path));
            _modContentPaths.ForEach(path => VerifyPath(path));
            VerifyPath(Constants.LogPath);

            StardewModdingAPI.Log.Initialize(Constants.LogPath);

            if (!File.Exists(Constants.ExecutionPath + "\\Stardew Valley.exe"))
            {
                StardewModdingAPI.Log.Error("Replace this");
                //throw new FileNotFoundException(string.Format("Could not found: {0}\\Stardew Valley.exe", Constants.ExecutionPath));
            }
        }
        
        /// <summary>
        /// Load Stardev Valley and control features
        /// </summary>
        private static void ConfigureSDV()
        {
            StardewModdingAPI.Log.Info("Initializing SDV Assembly...");

            // Load in the assembly - ignores security
            StardewAssembly.LoadStardewAssembly();
            StardewModdingAPI.Log.Comment("SDV Loaded Into Memory");

            // Change the game's version
            StardewModdingAPI.Log.Verbose("Injecting New SDV Version...");
            Game1.version += string.Format("-Z_MODDED | SMAPI {0}", Constants.VersionString);

            // Create the thread for the game to run in.
            Application.ThreadException += StardewModdingAPI.Log.Application_ThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += StardewModdingAPI.Log.CurrentDomain_UnhandledException;
                              

            //Create definition to listen for input
            StardewModdingAPI.Log.Verbose("Initializing Console Input Thread...");
            consoleInputThread = new Thread(ConsoleInputThread);
            
            Command.RegisterCommand("help", "Lists all commands | 'help <cmd>' returns command description").CommandFired += help_CommandFired;            

            StardewAssembly.Launch(); 
        }

        /// <summary>
        /// Wrap the 'RunGame' method for console output
        /// </summary>
        private static void GameRunInvoker()
        {
            //Game's in memory now, send the event
            StardewModdingAPI.Log.Verbose("Game Loaded");
            Events.GameEvents.InvokeGameLoaded();

            StardewModdingAPI.Log.Comment("Type 'help' for help, or 'help <cmd>' for a command's usage");
            //Begin listening to input
            consoleInputThread.Start();


            while (ready)
            {
                //Check if the game is still running 10 times a second
                Thread.Sleep(1000 / 10);
            }

            //abort the thread, we're closing
            if (consoleInputThread != null && consoleInputThread.ThreadState == ThreadState.Running)
                consoleInputThread.Abort();

            StardewModdingAPI.Log.Verbose("Game Execution Finished");
            StardewModdingAPI.Log.Verbose("Shutting Down...");
            Thread.Sleep(100);
            Environment.Exit(0);
        }

        /// <summary>
        /// Create the given directory path if it does not exist
        /// </summary>
        /// <param name="path">Desired directory path</param>
        private static void VerifyPath(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            catch (Exception ex)
            {
                StardewModdingAPI.Log.Error("Could not create a path: " + path + "\n\n" + ex);
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        static void StardewForm_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;

            if (true || MessageBox.Show("Are you sure you would like to quit Stardew Valley?\nUnsaved progress will be lost!", "Confirm Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
            {
                gamePtr.Exit();
                gamePtr.Dispose();
                StardewForm.Hide();
                ready = false;
            }
        }

        public static void LoadMods()
        {
            StardewModdingAPI.Log.Verbose("LOADING MODS");
            int loadedMods = 0;
            foreach (string ModPath in _modPaths)
            {
                foreach (String s in Directory.GetFiles(ModPath, "*.dll"))
                {
                    if (s.Contains("StardewInjector"))
                        continue;
                    StardewModdingAPI.Log.Success("Found DLL: " + s);
                    try
                    {
                        Assembly mod = Assembly.UnsafeLoadFrom(s); //to combat internet-downloaded DLLs

                        if (mod.DefinedTypes.Count(x => x.BaseType == typeof(Mod)) > 0)
                        {
                            StardewModdingAPI.Log.Verbose("Loading Mod DLL...");
                            TypeInfo tar = mod.DefinedTypes.First(x => x.BaseType == typeof(Mod));
                            Mod m = (Mod)mod.CreateInstance(tar.ToString());
                            Console.WriteLine("LOADED MOD: {0} by {1} - Version {2} | Description: {3}", m.Name, m.Authour, m.Version, m.Description);
                            loadedMods += 1;
                            m.Entry();
                        }
                        else
                        {
                            StardewModdingAPI.Log.Error("Invalid Mod DLL");
                        }
                    }
                    catch (Exception ex)
                    {
                        StardewModdingAPI.Log.Error("Failed to load mod '{0}'. Exception details:\n" + ex, s);
                    }
                }
            }
            StardewModdingAPI.Log.Success("LOADED {0} MODS", loadedMods);
        }

        public static void ConsoleInputThread()
        {
            string input = string.Empty;

            while (true)
            {
                Command.CallCommand(Console.ReadLine());
            }
        }
    
        public static void StardewInvoke(Action a)
        {
            StardewForm.Invoke(a);
        }

        static void help_CommandFired(object o, EventArgsCommand e)
        {
            if (e.Command.CalledArgs.Length > 0)
            {
                Command fnd = Command.FindCommand(e.Command.CalledArgs[0]);
                if (fnd == null)
                    StardewModdingAPI.Log.Error("The command specified could not be found");
                else
                {
                    if (fnd.CommandArgs.Length > 0)
                        StardewModdingAPI.Log.Info("{0}: {1} - {2}", fnd.CommandName, fnd.CommandDesc, fnd.CommandArgs.ToSingular());
                    else
                        StardewModdingAPI.Log.Info("{0}: {1}", fnd.CommandName, fnd.CommandDesc);
                }
            }
            else
                StardewModdingAPI.Log.Info("Commands: " + Command.RegisteredCommands.Select(x => x.CommandName).ToSingular());
        }

        #region Logging
        [Obsolete("This method is obsolete and will be removed in v0.39, please use the appropriate methods in the Log class")]
        public static void Log(object o, params object[] format)
        {
            StardewModdingAPI.Log.Info(o, format);
        }

        [Obsolete("This method is obsolete and will be removed in v0.39, please use the appropriate methods in the Log class")]
        public static void LogColour(ConsoleColor c, object o, params object[] format)
        {
            StardewModdingAPI.Log.Info(o, format);
        }

        [Obsolete("This method is obsolete and will be removed in v0.39, please use the appropriate methods in the Log class")]
        public static void LogInfo(object o, params object[] format)
        {
            StardewModdingAPI.Log.Info(o, format);
        }

        [Obsolete("This method is obsolete and will be removed in v0.39, please use the appropriate methods in the Log class")]
        public static void LogError(object o, params object[] format)
        {
            StardewModdingAPI.Log.Error(o, format);
        }

        [Obsolete("This method is obsolete and will be removed in v0.39, please use the appropriate methods in the Log class")]
        public static void LogDebug(object o, params object[] format)
        {
            StardewModdingAPI.Log.Debug(o, format);
        }

        [Obsolete("This method is obsolete and will be removed in v0.39, please use the appropriate methods in the Log class")]
        public static void LogValueNotSpecified()
        {
            StardewModdingAPI.Log.Error("<value> must be specified");
        }

        [Obsolete("This method is obsolete and will be removed in v0.39, please use the appropriate methods in the Log class")]
        public static void LogObjectValueNotSpecified()
        {
            StardewModdingAPI.Log.Error("<object> and <value> must be specified");
        }

        [Obsolete("This method is obsolete and will be removed in v0.39, please use the appropriate methods in the Log class")]
        public static void LogValueInvalid()
        {
            StardewModdingAPI.Log.Error("<value> is invalid");
        }

        [Obsolete("This method is obsolete and will be removed in v0.39, please use the appropriate methods in the Log class")]
        public static void LogObjectInvalid()
        {
            StardewModdingAPI.Log.Error("<object> is invalid");
        }

        [Obsolete("This method is obsolete and will be removed in v0.39, please use the appropriate methods in the Log class")]
        public static void LogValueNotInt32()
        {
            StardewModdingAPI.Log.Error("<value> must be a whole number (Int32)");
        }
        #endregion
    }
}