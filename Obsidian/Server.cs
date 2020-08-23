﻿using Newtonsoft.Json;
using Obsidian.BlockData;
using Obsidian.Chat;
using Obsidian.Commands;
using Obsidian.Commands.Parsers;
using Obsidian.Concurrency;
using Obsidian.Entities;
using Obsidian.Events;
using Obsidian.Events.EventArgs;
using Obsidian.Logging;
using Obsidian.Net.Packets;
using Obsidian.Net.Packets.Play;
using Obsidian.Plugins;
using Obsidian.Sounds;
using Obsidian.Util;
using Obsidian.Util.DataTypes;
using Obsidian.Util.Registry;
using Obsidian.World;
using Obsidian.World.Generators;
using Qmmands;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Obsidian
{
    public struct QueueChat
    {
        public string Message;
        public sbyte Position;
    }

    public class Server
    {
        private readonly ConcurrentQueue<QueueChat> chatmessages;
        private readonly ConcurrentQueue<PlayerDigging> diggers; // PETALUL this was unintended
        private readonly ConcurrentQueue<PlayerBlockPlacement> placed;
        private ConcurrentHashSet<Client> clients { get; }

        private readonly CancellationTokenSource cts;
        private readonly TcpListener tcpListener;

        public MinecraftEventHandler Events;
        public PluginManager PluginManager;
        public DateTimeOffset StartTime;

        public OperatorList Operators;

        public ConcurrentDictionary<string, Player> OnlinePlayers { get; } = new ConcurrentDictionary<string, Player>();

        public List<WorldGenerator> WorldGenerators { get; } = new List<WorldGenerator>();

        public WorldGenerator WorldGenerator { get; private set; }

        public CommandService Commands { get; }
        public Config Config { get; }
        public AsyncLogger Logger { get; }
        public int Id { get; private set; }
        public string Version { get; }
        public int Port { get; }
        public int TotalTicks { get; private set; }
        public World.World world;

        public string Path => System.IO.Path.GetFullPath(Id.ToString());

        /// <summary>
        /// Creates a new Server instance. Spawning multiple of these could make a multi-server setup  :thinking:
        /// </summary>
        /// <param name="version">Version the server is running.</param>
        public Server(Config config, string version, int serverId)
        {
            this.Config = config;

            this.Logger = new AsyncLogger($"Obsidian ID: {serverId}", Program.Config.LogLevel, System.IO.Path.Combine(Path, "latest.log"));

            this.Port = config.Port;
            this.Version = version;
            this.Id = serverId;

            this.tcpListener = new TcpListener(IPAddress.Any, this.Port);

            this.clients = new ConcurrentHashSet<Client>();

            this.cts = new CancellationTokenSource();
            this.chatmessages = new ConcurrentQueue<QueueChat>();
            this.diggers = new ConcurrentQueue<PlayerDigging>();
            this.placed = new ConcurrentQueue<PlayerBlockPlacement>();
            this.Commands = new CommandService(new CommandServiceConfiguration()
            {
                CaseSensitive = false,
                DefaultRunMode = RunMode.Parallel,
                IgnoreExtraArguments = true
            });
            this.Commands.AddModule<MainCommandModule>();
            this.Commands.AddTypeParser(new LocationTypeParser());
            this.Events = new MinecraftEventHandler();

            this.PluginManager = new PluginManager(this);
            this.Operators = new OperatorList(this);

            this.world = new World.World("", WorldGenerator);

            this.Events.PlayerLeave += this.Events_PlayerLeave;
            this.Events.PlayerJoin += this.Events_PlayerJoin;
        }

        private async Task ServerLoop()
        {
            var keepaliveticks = 0;
            while (!this.cts.IsCancellationRequested)
            {
                await Task.Delay(50);

                this.TotalTicks++;
                await this.Events.InvokeServerTickAsync();

                keepaliveticks++;
                if (keepaliveticks > 50)
                {
                    var keepaliveid = DateTime.Now.Millisecond;

                    foreach (var clnt in this.clients.Where(x => x.State == ClientState.Play).ToList())
                        _ = Task.Run(async () => { await clnt.ProcessKeepAlive(keepaliveid); });

                    keepaliveticks = 0;
                }

                if (this.chatmessages.TryDequeue(out QueueChat msg))
                {
                    foreach (var (_, player) in this.OnlinePlayers)
                    {
                        await player.SendMessageAsync(msg.Message, msg.Position);
                    }
                }

                foreach (var (uuid, player) in this.OnlinePlayers)
                {
                    if (this.Config.Baah.HasValue)
                    {
                        var pos = new SoundPosition(player.Transform.X, player.Transform.Y, player.Transform.Z);
                        await player.SendSoundAsync(461, pos, SoundCategory.Master, 1.0f, 1.0f);
                    }
                }

                if (this.diggers.TryDequeue(out PlayerDigging d))
                {
                    foreach (var clnt in clients)
                    {
                        var b = new BlockChange(d.Location, BlockRegistry.G(Materials.Air).Id);

                        await clnt.SendBlockChangeAsync(b);
                    }
                }

                foreach (var client in clients)
                {
                    if (!client.tcp.Connected)
                    {
                        this.clients.TryRemove(client);

                        continue;
                    }

                    //?
                    if (client.State == ClientState.Play)
                        await world.UpdateChunksForClientAsync(client);
                }
            }
        }

        public bool CheckPlayerOnline(string username) => this.clients.Any(x => x.Player != null && x.Player.Username == username);

        public void EnqueueDigging(PlayerDigging d)
        {
            this.diggers.Enqueue(d);
        }

        public async Task BroadcastBlockPlacementAsync(string senderId, PlayerBlockPlacement pbp)
        {
            foreach (var (uuid, player) in this.OnlinePlayers.Where(x => x.Key != senderId))
            {
                var client = player.client;//TODO

                var castedY = (long)pbp.Location.Y;
                Console.WriteLine($"\n\n\n\n\n Casted Y: {castedY}");

                var oldLoc = pbp.Location;
                pbp.Location.Y = castedY == oldLoc.Y ? castedY : oldLoc.Y + 1;

                var b = new BlockChange(pbp.Location, BlockRegistry.G(Materials.Cobblestone).Id);

               
                await client.SendBlockChangeAsync(b);
            }
        }

        public T LoadConfig<T>(Plugin plugin)
        {
            var path = GetConfigPath(plugin);

            if (!System.IO.File.Exists(path))
                SaveConfig(plugin, default(T));

            var json = System.IO.File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public void SaveConfig(Plugin plugin, object config)
        {
            var path = GetConfigPath(plugin);
            var json = JsonConvert.SerializeObject(config);
            System.IO.File.WriteAllText(path, json);
        }

        private string GetConfigPath(Plugin plugin)
        {
            var path = plugin.Path;
            var folder = System.IO.Path.GetDirectoryName(path);
            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            return System.IO.Path.Combine(folder, fileName) + ".json";
        }

        public async Task ParseMessage(string message, Client source, sbyte position = 0)
        {
            if (!CommandUtilities.HasPrefix(message, '/', out string output))
            {
                //await source.Player.SendMessageAsync($"<{source.Player.Username}> {message}", position);

                await this.BroadcastAsync($"<{source.Player.Username}> {message}", position);
                await Logger.LogMessageAsync($"<{source.Player.Username}> {message}");
                return;
            }

            var context = new CommandContext(source, this);
            IResult result = await Commands.ExecuteAsync(output, context);
            if (!result.IsSuccessful)
            {
                await context.Player.SendMessageAsync($"{ChatColor.Red}Command error: {(result as FailedResult).Reason}", position);
            }
        }

        public async Task BroadcastAsync(string message, sbyte position = 0)
        {
            chatmessages.Enqueue(new QueueChat() { Message = message, Position = position });
            await Logger.LogMessageAsync(message);
        }

        /// <summary>
        /// Starts this server
        /// </summary>
        /// <returns></returns>
        public async Task StartServer()
        {
            Console.CancelKeyPress += this.Console_CancelKeyPress;
            await this.Logger.LogMessageAsync($"Launching Obsidian Server v{Version} with ID {Id}");

            //Why?????
            //Check if MPDM and OM are enabled, if so, we can't handle connections 
            if (this.Config.MulitplayerDebugMode && this.Config.OnlineMode)
            {
                await this.Logger.LogErrorAsync("Incompatible Config: Multiplayer debug mode can't be enabled at the same time as online mode since usernames will be overwritten");
                this.StopServer();
                return;
            }

            await this.Logger.LogDebugAsync("Registering blocks..");
            await BlockRegistry.RegisterAll();

            await this.Logger.LogMessageAsync($"Loading operator list...");
            this.Operators.Initialize();

            await this.Logger.LogMessageAsync("Registering default entities");
            await this.RegisterDefaultAsync();

            await this.Logger.LogMessageAsync($"Loading and Initializing plugins...");
            await this.PluginManager.LoadPluginsAsync(this.Logger);

            if (this.WorldGenerators.FirstOrDefault(g => g.Id == this.Config.Generator) is WorldGenerator worldGenerator)
            {
                this.WorldGenerator = worldGenerator;
            }
            else
            {
                await this.Logger.LogWarningAsync($"Generator ({this.Config.Generator}) is unknown. Using default generator");
                this.WorldGenerator = new SuperflatGenerator();
            }

            await this.Logger.LogMessageAsync($"World generator set to {this.WorldGenerator.Id} ({this.WorldGenerator.ToString()})");

            await this.Logger.LogDebugAsync($"Set start DateTimeOffset for measuring uptime.");
            this.StartTime = DateTimeOffset.Now;

            await this.Logger.LogMessageAsync("Starting server backend...");
            await Task.Factory.StartNew(async () => { await this.ServerLoop().ConfigureAwait(false); });

            if (!this.Config.OnlineMode)
                await this.Logger.LogMessageAsync($"Server started in offline mode..");

            await this.Logger.LogDebugAsync($"Start listening for new clients");
            this.tcpListener.Start();

            while (!cts.IsCancellationRequested)
            {
                var tcp = await this.tcpListener.AcceptTcpClientAsync();

                await this.Logger.LogDebugAsync($"New connection from client with IP {tcp.Client.RemoteEndPoint}");

                int newplayerid = Math.Max(0, this.clients.Count);

                var clnt = new Client(tcp, this.Config, newplayerid, this);
                this.clients.Add(clnt);

                await Task.Factory.StartNew(async () => { await clnt.StartConnectionAsync().ConfigureAwait(false); });
            }

            await this.Logger.LogWarningAsync($"Cancellation has been requested. Stopping server...");
        }

        public async Task DisconnectIfConnectedAsync(string username, ChatMessage reason = null)
        {
            var player = this.OnlinePlayers.Values.FirstOrDefault(x => x.Username == username);
            if (player != null)
            {
                if (reason is null)
                    reason = ChatMessage.Simple("Connected from another location");

                await player.DisconnectAsync(reason);
            }
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            // TODO: TRY TO GRACEFULLY SHUT DOWN THE SERVER WE DONT WANT ERRORS REEEEEEEEEEE
            Console.WriteLine("shutting down..");
            StopServer();
        }

        public void StopServer()
        {
            this.WorldGenerators.Clear(); //Clean up for memory and next boot
            this.cts.Cancel();
        }

        /// <summary>
        /// Registers the "obsidian-vanilla" entities and objects
        /// </summary>
        private async Task RegisterDefaultAsync()
        {
            await RegisterAsync(new SuperflatGenerator());
            await RegisterAsync(new TestBlocksGenerator());
        }

        /// <summary>
        /// Registers a new entity to the server
        /// </summary>
        /// <param name="input">A compatible entry</param>
        /// <exception cref="Exception">Thrown if unknown/unhandable type has been passed</exception>
        public async Task RegisterAsync(params object[] input)
        {
            foreach (object item in input)
            {
                switch (item)
                {
                    default:
                        throw new Exception($"Input ({item.GetType()}) can't be handled by RegisterAsync.");

                    case WorldGenerator generator:
                        await Logger.LogDebugAsync($"Registering {generator.Id}...");
                        WorldGenerators.Add(generator);
                        break;
                }
            }
        }

        #region events
        private async Task Events_PlayerLeave(PlayerLeaveEventArgs e)
        {
            //TODO same here :)
            foreach (var other in this.OnlinePlayers.Values.Except(new List<Player> { e.WhoLeft }))
                await other.client.RemovePlayerFromListAsync(e.WhoLeft);

            await this.BroadcastAsync(string.Format(this.Config.LeaveMessage, e.WhoLeft.Username));
        }

        private async Task Events_PlayerJoin(PlayerJoinEventArgs e)
        {
            //TODO do this from somewhere else
            foreach (var other in this.OnlinePlayers.Values.Except(new List<Player> { e.Joined }))
                await other.client.AddPlayerToListAsync(e.Joined);

            await this.BroadcastAsync(string.Format(this.Config.JoinMessage, e.Joined.Username));
        }

        #endregion
    }
}