using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordIan.Service
{
    // Originally sourced from:
    //  https://github.com/discord-net/Discord.Net/blob/dev/samples/02_commands_framework/Services/CommandHandlingService.cs
    // License: MIT
    public class CommandHandlingService
    {
        private readonly IServiceProvider _services;
        private readonly CommandService _commands;
        private readonly DiscordSocketClient _discord;
        private readonly ILogger<CommandHandlingService> _logger;
        private readonly Model.Options _options;

        public CommandHandlingService(IServiceProvider services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _commands = services.GetRequiredService<CommandService>();
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _logger = services.GetRequiredService<ILogger<CommandHandlingService>>();
            _options = services.GetRequiredService<IOptionsMonitor<Model.Options>>().CurrentValue;

            // Hook CommandExecuted to handle post-command-execution logic.
            _commands.CommandExecuted += CommandExecutedAsync;

            // Hook MessageReceived so we can process each message to see
            // if it qualifies as a command.
            _discord.MessageReceived += MessageReceivedAsync;
        }

        public async Task InitializeAsync()
        {
            // Register modules that are public and inherit ModuleBase<T>.
            var assemblies = Assembly.GetExecutingAssembly().GetTypes()
                .Where(_ => _.IsClass && _.Namespace == nameof(DiscordIan));

            foreach (var assembly in assemblies)
            {
                _logger.LogDebug("Loading Discord modules in assembly {AssemblyName}",
                    assembly.Name);
                await _commands.AddModulesAsync(assembly.Assembly, _services);
            }
        }

        public async Task MessageReceivedAsync(SocketMessage message)
        {
            // Ignore system messages, or messages from other bots
            //if (!(rawMessage is SocketUserMessage message))
            //{
            //    return;
            //}

            // Ignore messages not from a user
            //if (message.Source != MessageSource.User)
            //{
            //    return;
            //}

            // This value holds the offset where the prefix ends
            var argPos = 0;
            // Perform prefix check. You may want to replace this with
            // (!message.HasCharPrefix('!', ref argPos))
            // for a more traditional command format like !help.
            if (!message.HasMentionPrefix(_discord.CurrentUser, ref argPos)
                && _options.IanCommandChar?.Length == 1
                && !message.HasCharPrefix(_options.IanCommandChar[0], ref argPos))
            {
                return;
            }

            var context = new SocketCommaContext(_discord, message);

            _logger.LogTrace("Received command from {Username} in message {Message}",
                context.User.Username,
                context.Message);

            // Perform the execution of the command. In this method,
            // the command service will perform precondition and parsing check
            // then execute the command if one is matched.
            await _commands.ExecuteAsync(context, argPos, _services);
        }

        public async Task CommandExecutedAsync(Optional<CommandInfo> command,
            ICommandContext context,
            IResult result)
        {
            // command is unspecified when there was a search failure (command not found)
            if (!command.IsSpecified)
            {
                _logger.LogDebug("Command not found in {Message}, requested by {Username}",
                    context?.Message?.Content,
                    context?.User?.Username);
                return;
            }

            // the command was successful
            if (result.IsSuccess)
            {
                _logger.LogTrace("Command success: {CommandName} for {Username}",
                    command.Value?.Name,
                    context.User.Username);
                return;
            }

            // the command failed, let's notify the user that something happened.
            _logger.LogWarning("Command failure: {CommandName} for {Username}: {Result}",
                command.Value?.Name,
                context.User.Username,
                result);
            await context.Channel.SendMessageAsync($"Error: {result}");
        }
    }
}
