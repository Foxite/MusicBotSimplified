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

			var discord = ProgramHost.Services.GetRequiredService<DiscordClient>();
			
			TwentyFourSevenConfig config247 = ProgramHost.Services.GetRequiredService<IOptions<TwentyFourSevenConfig>>().Value;
			
			discord.Ready += async (o, e) => {
				var notificationService = ProgramHost.Services.GetRequiredService<NotificationService>();
				await notificationService.SendNotificationAsync("Ready event."); // temporary, this is to see if the ready event fires more than once in the client lifetime
				StartPlaying(
					ProgramHost.Services.GetRequiredService<ILogger<Program>>(),
					notificationService,
					await discord.GetChannelAsync(config247.Channel) ?? throw new Exception("FUCK"),
					config247.Track,
					discord.GetVoiceNext()
				);
			};

			discord.UseVoiceNext();
			await discord.ConnectAsync();
			
			await ProgramHost.RunAsync();

			await discord.DisconnectAsync();
			//discord.Dispose();
		}

		private static void StartPlaying(ILogger logger, NotificationService notifications, DiscordChannel channel, string track, VoiceNextExtension voiceNextExtension) {
			_ = Task.Run(async () => {
				var lastError = DateTime.MinValue;
				int consecutiveErrors = 0;
				while (true) {
					await Task.Delay(TimeSpan.FromSeconds(1)); // prevents an error (idk)
					try {
						using VoiceNextConnection connection = voiceNextExtension.GetConnection(channel.Guild) ?? await channel.ConnectAsync();
						connection.VoiceSocketErrored += async (o, e) => {
							logger.LogError(e.Exception.Demystify(), "VoiceSocketErrored event");
							await notifications.SendNotificationAsync("VoiceSocketErrored event", e.Exception.Demystify());
						};

						await using Stream pcm = File.OpenRead(track);
						using VoiceTransmitSink transmit = connection.GetTransmitSink();
						while (true) {
							await pcm.CopyToAsync(transmit, 256 * transmit.SampleLength);
							pcm.Position = 0;
						}
					} catch (Exception e) {
						logger.LogCritical(e.Demystify(), "Error during playback");
						await notifications.SendNotificationAsync("Error during playback", e.Demystify());
						if ((DateTime.Now - lastError).TotalMinutes < 1) {
							consecutiveErrors++;
							if (consecutiveErrors > 5) {
								logger.LogCritical("Too many errors, restarting");
								await notifications.SendNotificationAsync("Too many errors, restarting");
								await ProgramHost.StopAsync();
								return;
							}
						} else {
							consecutiveErrors = 0;
						}
						lastError = DateTime.Now;
					}
				}
			});
		}
	}
}
