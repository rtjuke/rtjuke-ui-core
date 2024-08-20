using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RTJuke.UICore.Services
{
    /// <summary>
    /// Interface for classes which manage application updates
    /// </summary>
    public interface IUpdateService
    {
        Task<bool> CheckForUpdate();

        bool PresentUpdate(object windowOwner);

        void ExecuteUpdate();

        void Cleanup();

        void SetLocalization(string locale);
    }
}
