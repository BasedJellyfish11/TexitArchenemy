using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using JetBrains.Annotations;
using TexitArchenemy.Services.Logger;

namespace TexitArchenemy.Services.Discord.Commands;

public class Ask : ModuleBase<SocketCommandContext>
{
    private static readonly HttpClient Client = new();

    private struct GroqResponse
    {
        public required GroqChoices[] Choices { get; set; }

        internal struct GroqChoices
        {
            public required GroqMessage Message { get; set; }

            internal struct GroqMessage
            {
                public required string Content { get; set; }
            }
        }

    }

    [Command("Ask")]
    [Summary("Asks an LLM")]
    [UsedImplicitly]
    public async Task QueryLLM(params string[] originalMessage)
    {

        string message = string.Join(' ', originalMessage);        

        await ArchenemyLogger.Log($"Received Ask command with message {message}", "Discord");

        if (!Client.DefaultRequestHeaders.Contains("Authorization"))
            Client.DefaultRequestHeaders.Add("Authorization",
                $"Bearer {Environment.GetEnvironmentVariable("GROQ_TOKEN")}");
        
        
        string jsonContent = $$"""
                               {
                                 "model": "llama-3.3-70b-versatile",
                                 "messages": [
                                   {
                                     "role": "system",
                                     "content": "You are an LLM assistant answering questions in an informal context.\n ## Core Principles\nThe user can only interact with you once, so you don't prompt for follow ups or similar.\n CRITICAL: You always keep you messages under 2000 characters due to client side restrictions regarding maximum string size.\n Never use emojis unless specifically asked."
                                   },
                                   {
                                     "role": "user",
                                     "content": "{{message}}"
                                   }
                                 ]
                               }
                               """;

        StringContent content = new(jsonContent, Encoding.UTF8, "application/json");

        await ArchenemyLogger.Log($"Asking Llama", "Discord");

        HttpResponseMessage response = await Client.PostAsync(
            @"https://api.groq.com/openai/v1/chat/completions",
            content
        );
        
        // If rate limited (429), retry with qwen model
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            await ArchenemyLogger.Log("Llama Rate limited (429). Retrying with qwen model...", "Discord");

            string qwenJsonContent = $$"""
                                       {
                                         "model": "qwen/qwen3-32b",
                                         "messages": [
                                           {
                                             "role": "system",
                                             "content": "You are an LLM assistant answering questions in an informal context.\n ## Core Principles\nThe user can only interact with you once, so you don't prompt for follow ups or similar.\n CRITICAL: You always keep you messages under 2000 characters due to client side restrictions regarding maximum string size.\n Never use emojis unless specifically asked."
                                           },
                                           {
                                             "role": "user",
                                             "content": "{{message}}"
                                           }
                                         ]
                                       }
                                       """;

            StringContent qwenContent = new(qwenJsonContent, Encoding.UTF8, "application/json");

            response = await Client.PostAsync(
                @"https://api.groq.com/openai/v1/chat/completions",
                qwenContent
            );
        }

        GroqResponse? groqResponse = null;
        if (response.IsSuccessStatusCode)
        {
            groqResponse = await response.Content.ReadAsAsync<GroqResponse>();
        }

        await ArchenemyLogger.Log($"Ended Ask command successfully for message {message}", "Discord");
        await ReplyAsync(groqResponse?.Choices[0].Message.Content);
    }
    
}