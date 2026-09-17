using Microsoft.Extensions.DependencyInjection;
using Synerixis.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Infrastructure.Payment
{
    public class PaymentProviderFactory : IPaymentProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public PaymentProviderFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IPaymentProvider? GetProvider(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
                return null;

            return channel.Trim().ToLowerInvariant() switch
            {
                "wechat" => _serviceProvider.GetRequiredService<WechatPaymentProvider>(),
                "alipay" => _serviceProvider.GetRequiredService<AlipayPaymentProvider>(),
                _ => null
            };
        }
    }
}
