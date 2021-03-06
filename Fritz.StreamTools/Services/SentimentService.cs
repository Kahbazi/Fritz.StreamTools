﻿using Fritz.Chatbot.Commands;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Fritz.StreamTools.Services
{
	public class SentimentService : IHostedService
	{

		private readonly FollowerClient _followerClient;
		private bool _StopProcess = false;
		private TextAnalyticsClient _client;
		private static string _SubscriptionKey;

		private Dictionary<DateTime, (int count, double average)> _Observations = new Dictionary<DateTime, (int count, double average)>();

		private class ApiKeyServiceClientCredentials : ServiceClientCredentials
		{
			public override Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				request.Headers.Add("Ocp-Apim-Subscription-Key", _SubscriptionKey);
				return base.ProcessHttpRequestAsync(request, cancellationToken);
			}
		}

		public SentimentService(FollowerClient followerClient, IConfiguration config)
		{

			if (string.IsNullOrEmpty(config["FritzBot:SentimentAnalysisKey"]))
			{
				return;
			}

			_SubscriptionKey = config["FritzBot:SentimentAnalysisKey"].ToString();
			_followerClient = followerClient;
			_client = new TextAnalyticsClient(new ApiKeyServiceClientCredentials())
			{
				Endpoint = "https://centralus.api.cognitive.microsoft.com"
			};
		}


		public Task StartAsync(CancellationToken cancellationToken)
		{
			if (!string.IsNullOrEmpty(_SubscriptionKey)) Task.Run(async () => await Run());
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{

			_StopProcess = true;
			return Task.CompletedTask;

		}

		public async Task Run()
		{

			while (!_StopProcess)
			{

				if (SentimentSink.RecentChatMessages.Any())
				{

					var messageList = SentimentSink.RecentChatMessages.Select((value, index) => new MultiLanguageInput { Text = value, Id = index.ToString(), Language = "en" }).ToList();
					SentimentSink.RecentChatMessages.Clear();

					// Cheer 100 goranhal 25/8/19
					// Cheer 100 cadmus 25/8/19
					// Cheer 100 eternaldevcoder 25/8/19 

					SentimentBatchResult results = await _client.SentimentBatchAsync(new MultiLanguageBatchInput(messageList));

					var avgScore = results.Documents
						.Where(d => d.Score.HasValue)
						.Average(d => d.Score).Value;

					var now = DateTime.Now;
					_Observations.Add(now, (results.Documents.Count, avgScore));

					SentimentSink.SentimentInstant = avgScore;
					SentimentSink.Sentiment1Minute = CalculateSentimentOverLastMinutes(1);
					SentimentSink.Sentiment5Minute = CalculateSentimentOverLastMinutes(5);

					_followerClient.UpdateSentiment(SentimentSink.SentimentInstant,
						SentimentSink.Sentiment1Minute,
						SentimentSink.Sentiment5Minute,
						CalculateSentimentOverLastMinutes());

				}

				await Task.Delay(100);

			}

		}

		private double CalculateSentimentOverLastMinutes(int numMinutes = 0) {

			/*
			 * roberttables cheer 100 February 12, 2019
			 * ozcoder cheer 100 February 12, 2019
			 * jamesmontemagno cheer 100 February 12, 2019
			 */

			if (numMinutes > 0) {
				return _Observations.Where(o => o.Key > DateTime.Now.AddMinutes(-1 * numMinutes))
					.Average(v => v.Value.average);
			}

			return _Observations.Average(v => v.Value.average);

		}

	}
}
