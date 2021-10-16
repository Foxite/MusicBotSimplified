﻿using System;
using System.Diagnostics;
using System.IO;
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
	public static class Program {
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
			
			discord.Ready += (o, e) => {
				_ = Task.Run(async () => {
					await Task.Delay(TimeSpan.FromSeconds(1));
					try {
						DiscordChannel channel = await discord.GetChannelAsync(config247.Channel) ?? throw new Exception("FUCK");
						using VoiceNextConnection connection = await channel.ConnectAsync();
						connection.VoiceSocketErrored += (o, e) => {
							_ = host.Services.GetRequiredService<NotificationService>().SendNotificationAsync("VoiceSocketErrored event", e.Exception);
							return Task.CompletedTask;
						};
						
						using Process ffmpeg = Process.Start(new ProcessStartInfo {
							FileName = "ffmpeg",
							Arguments = $@"-stream_loop -1 -i ""{config247.Track}"" -ac 2 -f s16le -ar 48000 pipe:1",
							RedirectStandardOutput = true,
							UseShellExecute = false
						}) ?? throw new Exception("you forgot to install ffmpeg you moron");

						await using Stream pcm = ffmpeg.StandardOutput.BaseStream;
						using VoiceTransmitSink transmit = connection.GetTransmitSink();
						await pcm.CopyToAsync(transmit);
					} catch (Exception e) {
						Console.WriteLine(e.ToStringDemystified());
					}
				});
				return Task.CompletedTask;
			};
			
			await discord.ConnectAsync();
			
			await host.RunAsync();

			await discord.DisconnectAsync();
			//discord.Dispose();
		}
	}
}
