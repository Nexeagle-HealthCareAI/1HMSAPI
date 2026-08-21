using System.Collections.Generic;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    public interface ITranslationService
    {
        Task<string> TranslateTextAsync(string text, string targetLanguage);
        Task<Dictionary<string, string>> TranslateMultipleAsync(Dictionary<string, string> texts, string targetLanguage);
    }
}
