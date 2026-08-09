using System.Runtime.CompilerServices;
using System.Text;

namespace FileCompare.Services;

internal static class EncodingRegistration
{
#pragma warning disable CA2255 // intentional: registers Windows-1252 support for every consumer of this library
    [ModuleInitializer]
    public static void RegisterCodePages()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
#pragma warning restore CA2255
}
