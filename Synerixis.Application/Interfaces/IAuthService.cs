using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Application.Interfaces
{
    public interface IAuthService
    {
        string GenerateJwt(Guid userId, string role);
        // 未来可扩展：RefreshToken、ValidateToken 等
    }
}
