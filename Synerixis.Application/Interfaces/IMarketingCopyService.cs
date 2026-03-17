using System.Threading.Tasks;
using Synerixis.Application.DTOs;
using Synerixis.Domain.Entities;

namespace Synerixis.Application.Interfaces
{
    /// <summary>
    /// Defines the contract for the marketing copy generation service.
    /// </summary>
    public interface IMarketingCopyService
    {
        /// <summary>
        /// Generates marketing copy based on the provided input.
        /// </summary>
        /// <param name="generateCopyDto">The DTO containing product information and generation parameters.</param>
        /// <returns>A new MarketingCopy entity.</returns>
        Task<MarketingCopy> GenerateCopyAsync(GenerateCopyDto generateCopyDto);
    }
}
