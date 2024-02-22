using System;
using System.Collections.Generic;
using System.Reflection;

namespace KNSoft.C4Lib;
public static class MetaInfo
{
    public static readonly Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
    public static readonly Assembly CallingAssembly = Assembly.GetCallingAssembly();

    public static readonly String? ProductName = CurrentAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
    public static readonly String? CompanyName = CurrentAssembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
    public static readonly String? FileVersion = CurrentAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
    public static readonly String? Version = CurrentAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    public static readonly String? Configuration = CurrentAssembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
    public static readonly String? Description = CurrentAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
    public static readonly IEnumerable<AssemblyMetadataAttribute> Metadata = CurrentAssembly.GetCustomAttributes<AssemblyMetadataAttribute>();
    public static String? FindMetadata(String Key) => Rtl.EnumerableFirstOrNull(Metadata, x => x.Key == Key)?.Value;
}
