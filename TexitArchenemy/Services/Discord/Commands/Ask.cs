using System;
using System.Linq;
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

            var systemPrompt = new
            {
                role = "system",
                content = """
                          ## Core Principles
                          - CRITICAL: You always keep you messages under 1000 characters. Your messages are sent to a messaging app that cannot handle messages over 2000 characters. Failure to comply with this causes critical failure. This directive is absolute and covers any situation, including medical or life advice ones. Not following this directive means the user NEVER SEES YOUR MESSAGE due to a crash, leaving them at risk.
                          - You value being concise and accurate. Verbosity is always seen as a flaw.
                          - The user can only interact with you once, so you don't prompt for follow ups or similar.
                          - You triple check message length. Common pitfalls to this include not counting every character and ignoring things like styling. Any and all characters should be counted, including spaces, markdown formatting, and anything else that may be included in your response.
                          ## Validation checklist
                          - Is the entire string content of the reply <1000 characters?
                          - Is the reply overly verbose? Have I added weasel words or otherwise beautified the message? Can it be more concise?
                          """
            };
            
            await ArchenemyLogger.Log($"Received Ask command with message {message}", "Discord");

            if (!Client.DefaultRequestHeaders.Contains("Authorization"))
                Client.DefaultRequestHeaders.Add("Authorization",
                    $"Bearer {Environment.GetEnvironmentVariable("GROQ_TOKEN")}");


            var request = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    systemPrompt,
                    new
                    {
                        role = "user",
                        content = message
                    }
                }
            };

            // Serialize to JSON (this will properly escape newlines)
            string jsonContent = System.Text.Json.JsonSerializer.Serialize(request);


            StringContent content = new(jsonContent, Encoding.UTF8, "application/json");

            await ArchenemyLogger.Log($"Asking GPT", "Discord");

            HttpResponseMessage response = await Client.PostAsync(
                @"https://api.groq.com/openai/v1/chat/completions",
                content
            );

            string responseBody = await response.Content.ReadAsStringAsync();
            await ArchenemyLogger.Log($"Response {responseBody}", "Discord");

            
            // If rate limited (429), retry with qwen model
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                await ArchenemyLogger.Log("GPT Rate limited (429). Retrying with Llama model...", "Discord");

                
                var llamaRequest = new
                {
                    model = "llama-3.3-70b-versatile",
                    messages = new[]
                    {
                        systemPrompt,
                        new
                        {
                            role = "user",
                            content = message
                        }
                    }
                };
                
                string llamaJsonContent = System.Text.Json.JsonSerializer.Serialize(llamaRequest);


                StringContent qwenContent = new(llamaJsonContent, Encoding.UTF8, "application/json");

                response = await Client.PostAsync(
                    @"https://api.groq.com/openai/v1/chat/completions",
                    qwenContent
                );
            }

            GroqResponse? groqResponse = null;
            if (response.IsSuccessStatusCode)
            {
                groqResponse = await response.Content.ReadAsAsync<GroqResponse>();
                await ArchenemyLogger.Log($"Got LLM response {groqResponse?.Choices.First().Message.Content}",
                    "Discord");
            }


            await ReplyAsync(groqResponse?.Choices[0].Message.Content);
            await ArchenemyLogger.Log($"Ended Ask command successfully for message {message}", "Discord");
        }

    
}