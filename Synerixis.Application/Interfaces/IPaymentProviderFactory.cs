using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Application.Interfaces
{
    public interface IPaymentProviderFactory
    {
        IPaymentProvider? GetProvider(string channel);
    }
}
