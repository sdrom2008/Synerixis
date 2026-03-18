using Synerixis.Domain.Enums;
using Synerixis.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Application.Interfaces
{
    public interface IAgentRouter
    {
        IAgent? GetAgent(ChatIntent intent);
        Task<AgentProcessResult> RouteAsync(ChatIntent intent, string userInput, ChatContext context);
    }
}
