// File: src/NetworkChecker/Gui/AppFonts.cs
using NetworkChecker.Core;

namespace NetworkChecker.Gui;

/// <summary>
/// 表示言語に応じた既定フォントを作成する。
/// フォントがインストールされていない環境でも例外で落ちないよう、候補を順に試す。
/// </summary>
internal static class AppFonts
{
    public static Font Create()
    {
        var candidates = Strings.Current == "ja"
            ? new[] { "Yu Gothic UI", "Meiryo UI", "Segoe UI" }
            : new[] { "Segoe UI", "Yu Gothic UI", "Meiryo UI" };

        foreach (var name in candidates)
        {
            try
            {
                return new Font(name, 9f);
            }
            catch (ArgumentException)
            {
                // フォント未インストール → 次の候補へ
            }
        }
        return SystemFonts.DefaultFont;
    }
}
