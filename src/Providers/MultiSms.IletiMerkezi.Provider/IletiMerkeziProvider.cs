﻿using System.Text;
using MultiSms.Helpers;
using MultiSms.IletiMerkezi.Provider.Models;
using MultiSms.IletiMerkezi.Provider.Options;
using MultiSms.Interfaces;
using MultiSms.Models;
using Newtonsoft.Json;

namespace MultiSms.IletiMerkezi.Provider;

public partial class IletiMerkeziProvider : IIletiMerkeziProvider
{
    public SendingResult Send(MessageBody message)
    {
        return SendAsync(message).GetAwaiter().GetResult();
    }

    public async Task<SendingResult> SendAsync(MessageBody message, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Post, new UriBuilder(_options.BaseUrl) { Path = "v1/send-sms/json" }.Uri);
            using var jsonContent = new StringContent(JsonConvert.SerializeObject(CreateMessage(message)), Encoding.UTF8, "application/json");

            request.Content = jsonContent;

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            return BuildResultObject(response);
        }
        catch (Exception ex)
        {
            return SendingResult.Failure(Name).AddError(ex);
        }
    }
}

public partial class IletiMerkeziProvider
{
    private readonly HttpClient _httpClient;
    private readonly IletiMerkeziProviderOptions _options;

    string ISmsProvider.Name => Name;

    public const string Name = "IletiMerkezi";

    public IletiMerkeziProvider(HttpClient httpClient, IletiMerkeziProviderOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.Validate();
        _options = options;

        _httpClient = httpClient ?? new HttpClient();
    }

    private HttpClient CreateClient() => _httpClient;

    private static SendingResult BuildResultObject(HttpResponseMessage result)
    {
        return result.IsSuccessStatusCode
            ? SendingResult.Success(Name).AddMetaData("response", result)
            : SendingResult.Failure(Name).AddError(new SendingError(result.StatusCode.ToString(), result.ReasonPhrase));
    }

    public IletiMerkeziMessage CreateMessage(MessageBody message)
    {
        var data = message.ProviderData;
        var keyProviderData = data.GetData(CustomProviderData.Key);
        var hashProviderData = data.GetData(CustomProviderData.Hash);
        var orginatorProviderData = data.GetData(CustomProviderData.Orginator);

        var key = keyProviderData.IsEmpty() ? _options.Key : keyProviderData.GetValue<string>();
        var hash = hashProviderData.IsEmpty() ? _options.Hash : hashProviderData.GetValue<string>();
        var orginator = orginatorProviderData.IsEmpty() ? _options.Orginator : orginatorProviderData.GetValue<string>();

        var option = new IletiMerkeziMessage
        {
            request = new Request
            {
                authentication = new Authentication
                {
                    key = key,
                    hash = hash
                },
                order = new Order
                {
                    sender = orginator,
                    message = new Message
                    {
                        text = message.Content,
                        receipts = new Receipts
                        {
                            number = new List<string>()
                        {
                            message.To
                        }
                        }
                    }
                }
            }
        };

        return option;
    }
}