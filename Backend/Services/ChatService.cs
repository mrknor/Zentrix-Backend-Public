using System.Threading.Tasks;
using Backend.Services;

namespace Backend.Services
{
    public class ChatService
    {
        private readonly OpenAIClient _openAiClient;

        public ChatService(OpenAIClient openAiClient)
        {
            _openAiClient = openAiClient;
        }

        public async Task<ChatResult> HandleChatMessageAsync(string threadId, string userId, string assistantId, string message)
        {
            // If threadId is null or empty, create a new thread
            if (string.IsNullOrEmpty(threadId))
            {
                threadId = await _openAiClient.CreateThreadAsync();
                if (string.IsNullOrEmpty(threadId))
                {
                    return new ChatResult { Message = "Failed to create a conversation thread." };
                }
            }

            // Add the user's message to the thread
            var messageId = await _openAiClient.AddMessageToThreadAsync(threadId, message);
            if (string.IsNullOrEmpty(messageId))
            {
                return new ChatResult { Message = "Failed to send the message to the assistant." };
            }

            // Create a run in the thread with the specified assistant ID
            await _openAiClient.CreateRunAsync(threadId, assistantId, message);

            // Wait for the assistant's response
            var assistantResponse = await _openAiClient.WaitForAssistantResponseAsync(threadId, assistantId, messageId);

            if (string.IsNullOrEmpty(assistantResponse))
            {
                return new ChatResult { Message = "The assistant did not provide a response." };
            }

            // Return the assistant's response without additional processing
            return new ChatResult
            {
                Message = assistantResponse,
                ThreadId = threadId
            };
        }
    }

    public class ChatResult
    {
        public string ThreadId { get; set; }
        public string Message { get; set; }
    }
}
