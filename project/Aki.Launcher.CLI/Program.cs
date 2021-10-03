﻿using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Aki.Launcher.CLI.Helpers;

namespace Aki.Launcher.CLI
{
    class LoginCredentials
    {
        public string ServerAddress { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public async Task Connect()
        {
            if (ServerManager.SelectedServer?.backendUrl == ServerAddress)
                return;
            
            await ServerManager.LoadDefaultServerAsync(ServerAddress);
        }

        public async Task Login()
        {
            await Connect();

            Prompt();
            
            var result = await AccountManager.LoginAsync(Username, Password);
            if (result < 0)
            {
                Console.WriteLine($"Failed to login: {result}");
                Environment.Exit(1);
            }
        }

        public void Prompt()
        {
            if (string.IsNullOrEmpty(Username))
            {
                Console.Write("Username: ");
                Username = Console.ReadLine();
            }

            if (string.IsNullOrEmpty(Password))
            {
                Console.Write("Password: ");
                Password = CLIHelper.ReadPassword();
            }
        }
    }

    class GameOptions
    {
        public string OriginalGameDirectory { get; set; }
        public string GameDirectory { get; set; }
    }
    
    class Program
    {
        private static async Task<int> Launch(LoginCredentials login, GameOptions gameOptions)
        {
            await login.Login();

            var starter = new GameStarter(new GameStarterFrontendCLI(),
                gamePath: gameOptions.GameDirectory,
                originalGamePath: gameOptions.OriginalGameDirectory);
            var result = await starter.LaunchGame(ServerManager.SelectedServer, AccountManager.SelectedAccount);
            if (!result.Succeeded)
            {
                Console.WriteLine($"Failed to launch game: {result.Message}");
            }
            
            return 0;
        }

        private static async Task<int> Register(LoginCredentials login, string edition)
        {
            await login.Connect();
            login.Prompt();

            var result = await AccountManager.RegisterAsync(login.Username, login.Password, edition);
            if (result < 0)
            {
                Console.WriteLine($"Failed to register account: {result}");
                return 1;
            }

            return 0;
        }

        public static void Main(string[] args)
        {
            var registerCommand = new Command("register", "Register a new profile")
            {
                new Option<string>("--edition",
                    () => "Edge of Darkness",
                    "The game edition to assign to this profile"),
            };
            registerCommand.Handler = CommandHandler.Create<LoginCredentials, string>(Register);

            var launchCommand = new Command("launch", "Launch Tarkov single-player")
            {
                new Option<bool>("--show-only", "Only show the command to run"),
            };
            launchCommand.Handler = CommandHandler.Create<LoginCredentials, GameOptions>(Launch);

            var rootCommand = new RootCommand
            {
                registerCommand,
                launchCommand,
            };
            rootCommand.AddGlobalOption(new Option<string>(new[] {"-u", "--username"}, "Username to login as"));
            rootCommand.AddGlobalOption(new Option<string>(new[] {"-p", "--password"}, "Password for user to login as"));
            rootCommand.AddGlobalOption(new Option<string>(new[] {"-s", "--server-address"},
                                                            () => "https://localhost:8443",
                                                            "Address to contact Server on"));
            rootCommand.AddGlobalOption(new Option<string>(new[] {"--game-dir", "--game-directory"}, "The target game directory"));
            rootCommand.AddGlobalOption(new Option<string>(new[] {"--original-game-dir", "--original-game-directory"},
                "The directory of Tarkov downloaded from the BSG launcher"));
            
            rootCommand.Handler = CommandHandler.Create<string>(async (serverAddress) =>
            {
                Console.WriteLine("connecting");
                await ServerManager.LoadDefaultServerAsync(serverAddress);
            });

            Environment.Exit(rootCommand.Invoke(args));
        }
    }
}