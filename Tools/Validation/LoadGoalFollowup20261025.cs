var compiler = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.Pipeline.Compilation.RoslynCompilationService")).First(t => t != null);
var requestType = compiler.Assembly.GetType("Unity.Pipeline.Compilation.CompilationRequest");
var outputs = new List<object>();
foreach (var file in new[] { "Tools/Validation/GoalSession20261009.cs", "Tools/Validation/UiProfile20261021.cs" })
{
    var request = Activator.CreateInstance(requestType);
    requestType.GetProperty("SourceCode").SetValue(request, System.IO.File.ReadAllText(file, System.Text.Encoding.UTF8));
    requestType.GetProperty("AssemblyName").SetValue(request, "GoalFollowup_" + Guid.NewGuid().ToString("N"));
    var compiled = compiler.GetMethod("Compile").Invoke(null, new[] { request });
    if (!(bool)compiled.GetType().GetProperty("Success").GetValue(compiled)) return compiled;
    outputs.Add(file);
}
return outputs;
