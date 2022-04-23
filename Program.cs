using System;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IkIheMusicBotSimplified {
	public sealed class Program {
		private static IHost ProgramHost { get; set; }
		
		private static async Task Main(string[] args) {
			ProgramHost = Host.CreateDefaultBuilder()
				.UseConsoleLifetime()
				.ConfigureAppConfiguration(configBuilder => {
					configBuilder.SetBasePath(Directory.GetCurrentDirectory());
					configBuilder.AddNewtonsoftJsonFile("appsettings.json");
					configBuilder.AddEnvironmentVariables("IKIHE_");
					configBuilder.AddCommandLine(args);
				})
				.ConfigureServices((ctx, isc) => {
					isc.AddSingleton(isp => new DiscordClient(new DiscordConfiguration() {
						Token = Environment.GetEnvironmentVariable("BOT_TOKEN"),
						LoggerFactory = isp.GetRequiredService<ILoggerFactory>()
					}));
				})
				.Build();

			var discord = ProgramHost.Services.GetRequiredService<DiscordClient>();

			ulong channelId = ulong.Parse(Environment.GetEnvironmentVariable("CHANNEL_ID"));
			string track = Environment.GetEnvironmentVariable("TRACK");
			
			discord.Ready += async (o, e) => {
				StartPlaying(await discord.GetChannelAsync(channelId), track, discord.GetVoiceNext());
			};

			discord.UseVoiceNext();
			await discord.ConnectAsync();
			
			await ProgramHost.RunAsync();

			await discord.DisconnectAsync();
			//discord.Dispose();
		}

		private static void StartPlaying(DiscordChannel channel, string track, VoiceNextExtension voiceNextExtension) {
			_ = Task.Run(async () => {
				while (true) {
					await Task.Delay(TimeSpan.FromSeconds(1)); // prevents an error (idk)
					using VoiceNextConnection connection = voiceNextExtension.GetConnection(channel.Guild) ?? await channel.ConnectAsync();
					await using Stream pcm = File.OpenRead(track);
					using VoiceTransmitSink transmit = connection.GetTransmitSink();
					while (true) {
						await pcm.CopyToAsync(transmit, 256 * transmit.SampleLength);
						pcm.Position = 0;
					}
				}
			});
		}
	}
}
