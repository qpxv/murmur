using System.Runtime.InteropServices;

// every p/invoke here targets a windows system dll: never load one from the exe's folder
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
