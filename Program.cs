using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using Foxite.Common.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IkIheMusicBotSimplified {
	public sealed class Program {
		private static async Task Main(string[] args) {
			IHost host = Host.CreateDefaultBuilder()
				.UseConsoleLifetime()
				.ConfigureAppConfiguration(configBuilder => {
					configBuilder.SetBasePath(Directory.GetCurrentDirectory());
					configBuilder.AddNewtonsoftJsonFile("appsettings.json");
					configBuilder.AddEnvironmentVariables("IKIHE_");
					configBuilder.AddCommandLine(args);
				})
				.ConfigureServices((ctx, isc) => {
					isc.Configure<DiscordConfiguration>(ctx.Configuration.GetSection(nameof(DiscordConfiguration)));
					isc.Configure<TwentyFourSevenConfig>(ctx.Configuration.GetSection(nameof(TwentyFourSevenConfig)));
					
					isc.AddNotifications()
						.AddDiscord(ctx.Configuration.GetSection("Notifications"));

					isc.AddSingleton(isp => new DiscordClient(new DSharpPlus.DiscordConfiguration() {
						Token = isp.GetRequiredService<IOptions<DiscordConfiguration>>().Value.Token,
						LoggerFactory = isp.GetRequiredService<ILoggerFactory>()
					}));
				})
				.Build();

			var discord = host.Services.GetRequiredService<DiscordClient>();
			TwentyFourSevenConfig config247 = host.Services.GetRequiredService<IOptions<TwentyFourSevenConfig>>().Value;
			
			discord.Ready += async (o, e) => {
				StartPlaying(
					host.Services.GetRequiredService<ILogger<Program>>(),
					host.Services.GetRequiredService<NotificationService>(),
					await discord.GetChannelAsync(config247.Channel) ?? throw new Exception("FUCK"),
					config247.Track
				);
			};

			discord.UseVoiceNext();
			await discord.ConnectAsync();
			
			await host.RunAsync();

			await discord.DisconnectAsync();
			//discord.Dispose();
		}

		private static void StartPlaying(ILogger logger, NotificationService notifications, DiscordChannel channel, string track) {
			_ = Task.Run(async () => {
				await Task.Delay(TimeSpan.FromSeconds(1)); // prevents an error (idk)
				bool reportError = true;
				try {
					var cts = new CancellationTokenSource();
					using VoiceNextConnection connection = await channel.ConnectAsync();
					connection.VoiceSocketErrored += async (o, e) => {
						reportError = false;
						cts.Cancel();
						logger.LogCritical(e.Exception.Demystify(), "VoiceSocketErrored event, attempting to reconnect");
						await notifications.SendNotificationAsync("VoiceSocketErrored event, attempting to reconnect", e.Exception.Demystify());
					};

					await using Stream pcm = File.OpenRead(track);
					using VoiceTransmitSink transmit = connection.GetTransmitSink();
					while (!cts.IsCancellationRequested) {
						await pcm.CopyToAsync(transmit, 256 * transmit.SampleLength, cts.Token);
						pcm.Position = 0;
					}
				} catch (Exception e) {
					if (reportError) {
						logger.LogCritical(e.Demystify(), "Error during playback, attempting to reconnect");
						await notifications.SendNotificationAsync("VoiceSocketErrored event", e.Demystify());
					}
					StartPlaying(logger, notifications, channel, track);
				}
			});
		}
	}
}
