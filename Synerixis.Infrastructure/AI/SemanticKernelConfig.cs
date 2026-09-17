using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Synerixis.Infrastructure.AI
{
    /// <summary>
    /// 兼容旧 DI：委托 <see cref="LlmRuntime"/>。无 Key 时不在构造期抛错。
    /// </summary>
    public class SemanticKernelConfig
    {
        private readonly LlmRuntime _runtime;

        public SemanticKernelConfig(LlmRuntime runtime)
        {
            _runtime = runtime;
        }

        public bool IsConfigured => _runtime.IsConfigured;

        public Kernel Kernel =>
            _runtime.IsConfigured
                ? _runtime.GetKernel()
                : Kernel.CreateBuilder().Build();
    }
}
