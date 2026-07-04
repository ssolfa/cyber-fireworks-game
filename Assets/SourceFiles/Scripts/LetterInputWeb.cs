using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// Bridges Unity to the browser's native text prompt so Korean (and any language)
// input works correctly in WebGL builds — Unity's legacy InputField cannot do
// proper Korean IME composition. In the editor this is a no-op (use the fallback
// panel for testing). Lives on a uniquely-named GameObject for SendMessage.
public class LetterInputWeb : MonoBehaviour
{
    public LetterFireworks letterFw;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WebPromptSend(string obj, string method, string msg, string def, string placeholder);
#endif

    public static bool IsWebGL
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    // Opens the browser prompt; the entered text comes back via OnKoreanInput.
    public void Ask()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebPromptSend(gameObject.name, "OnKoreanInput",
            Loc.T("불꽃으로 만들 글자를 입력하세요", "Type letters to shoot as fireworks"),
            "",
            Loc.T("예: 사랑해 / I LOVE U", "e.g. I LOVE U"));
#endif
    }

    // Called by the browser (SendMessage) with the entered string.
    public void OnKoreanInput(string text)
    {
        if (letterFw != null) letterFw.Spell(text);
    }
}
