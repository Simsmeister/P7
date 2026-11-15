using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

[InitializeOnLoad]
static class CompileLog
{
    const string FilePath = "Temp/compile-log.txt";

    static CompileLog()
    {
        CompilationPipeline.compilationStarted += _ => File.WriteAllText(FilePath, "STARTED\n");
        CompilationPipeline.assemblyCompilationFinished += (assembly, messages) =>
        {
            foreach (var m in messages)
                if (m.type == CompilerMessageType.Error)
                    File.AppendAllText(FilePath, m.message + "\n");
        };
        CompilationPipeline.compilationFinished += _ => File.AppendAllText(FilePath, "FINISHED\n");
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception)
                File.AppendAllText("Temp/console-errors.txt", type + ": " + message + "\n" + stack + "\n");
        };
        File.AppendAllText("Temp/compile-log.txt", "DOMAIN RELOADED\n");
    }
}
