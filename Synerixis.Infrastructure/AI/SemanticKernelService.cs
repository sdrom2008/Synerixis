using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Synerixis.Infrastructure.AI
{
    /// <summary>
    /// 兼容旧 DI：经 <see cref="LlmRuntime"/> 取 Chat。无 Key 时 GetChatService 抛「未配置 AI」。
    /// </summary>
    public class SemanticKernelService
    {
        private readonly LlmRuntime _runtime;

        public SemanticKernelService(LlmRuntime runtime)
        {
            _runtime = runtime;
        }

        public bool IsConfigured => _runtime.IsConfigured;

        public Kernel GetKernel() => _runtime.GetKernel();

        public IChatCompletionService GetChatService() => _runtime.GetChatService();
    }
}
