using System;
using System.Collections;
using System.Linq;
using UnityEngine;

// Captures the already-operated screen at a legal render boundary. Does not send input.
public static class NativeUiCapture20261021
{
    public static string Main(string name)
    {
        var host=new GameObject("NativeUiCapture_TEMP"){hideFlags=HideFlags.DontSave};
        host.AddComponent<NativeUiCaptureRunner20261021>().StartCoroutine(Capture(host,name));
        return "Capture scheduled after native input: "+name;
    }
    static IEnumerator Capture(GameObject host,string name)
    {
        yield return new WaitForEndOfFrame();
        try
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("GoalSession20261009")).First(t=>t!=null);
            type.GetMethod("CaptureInteractive").Invoke(null,new object[]{name});
        }
        finally{UnityEngine.Object.Destroy(host);}
    }
}
public sealed class NativeUiCaptureRunner20261021:MonoBehaviour{}
