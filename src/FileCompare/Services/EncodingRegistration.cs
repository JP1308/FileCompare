using System.Runtime.CompilerServices;
using System.Text;

namespace FileCompare.Services;

internal static class EncodingRegistration
{
    [ModuleInitializer]
    public static void RegisterCodePages()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
