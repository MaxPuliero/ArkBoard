using System.Collections.Generic;
using System.Globalization;

namespace ArkBoard
{
    internal enum UiLanguage { English, Italian, Japanese }

    internal static class Localization
    {
        internal static UiLanguage Current = UiLanguage.English;
        static readonly Dictionary<string, string[]> Values = new Dictionary<string, string[]>
        {
            { "_File", new[]{ "_File", "_File", "ファイル(_F)" } },
            { "_Edit", new[]{ "_Edit", "_Modifica", "編集(_E)" } },
            { "_View", new[]{ "_View", "_Visualizza", "表示(_V)" } },
            { "_Settings", new[]{ "_Settings", "_Impostazioni", "設定(_S)" } },
            { "_Help", new[]{ "_Help", "_Aiuto", "ヘルプ(_H)" } },
            { "New Project", new[]{ "New Project", "Nuovo progetto", "新規プロジェクト" } },
            { "Open Project...", new[]{ "Open Project...", "Apri progetto...", "プロジェクトを開く..." } },
            { "Save", new[]{ "Save", "Salva", "保存" } },
            { "Save As...", new[]{ "Save As...", "Salva con nome...", "名前を付けて保存..." } },
            { "Save Project", new[]{ "Save Project", "Salva progetto", "プロジェクトを保存" } },
            { "Import Images...", new[]{ "Import Images...", "Importa immagini...", "画像を読み込む..." } },
            { "Exit", new[]{ "Exit", "Esci", "終了" } },
            { "Undo", new[]{ "Undo", "Annulla", "元に戻す" } },
            { "Redo", new[]{ "Redo", "Ripeti", "やり直す" } },
            { "Copy Image", new[]{ "Copy Image", "Copia immagine", "画像をコピー" } },
            { "Paste", new[]{ "Paste", "Incolla", "貼り付け" } },
            { "Duplicate", new[]{ "Duplicate", "Duplica", "複製" } },
            { "Duplicate Selection", new[]{ "Duplicate Selection", "Duplica selezione", "選択を複製" } },
            { "Select / Deselect All", new[]{ "Select / Deselect All", "Seleziona / deseleziona tutto", "すべて選択 / 解除" } },
            { "Normalize Size", new[]{ "Normalize Size", "Uniforma dimensioni", "サイズを揃える" } },
            { "Pack Images", new[]{ "Pack Images", "Compatta immagini", "画像を整列" } },
            { "Reset Scale", new[]{ "Reset Scale", "Reimposta scala", "スケールをリセット" } },
            { "Reset Rotation", new[]{ "Reset Rotation", "Reimposta rotazione", "回転をリセット" } },
            { "Reset Mask", new[]{ "Reset Mask", "Reimposta maschera", "マスクをリセット" } },
            { "Delete Selection", new[]{ "Delete Selection", "Elimina selezione", "選択を削除" } },
            { "Delete", new[]{ "Delete", "Elimina", "削除" } },
            { "Fit All", new[]{ "Fit All", "Adatta tutto", "全体表示" } },
            { "Fit Selection", new[]{ "Fit Selection", "Adatta selezione", "選択範囲を表示" } },
            { "Zoom 100%", new[]{ "Zoom 100%", "Zoom 100%", "ズーム 100%" } },
            { "Opacity 100%", new[]{ "Opacity 100%", "Opacità 100%", "不透明度 100%" } },
            { "Grid", new[]{ "Grid", "Griglia", "グリッド" } },
            { "Always on Top", new[]{ "Always on Top", "Sempre in primo piano", "常に手前に表示" } },
            { "Auto-Sorting", new[]{ "Auto-Sorting", "Ordinamento automatico", "自動並べ替え" } },
            { "Invert Alt + Middle Drag Zoom", new[]{ "Invert Alt + Middle Drag Zoom", "Inverti zoom Alt + trascinamento centrale", "Alt＋中ドラッグのズームを反転" } },
            { "Language", new[]{ "Language", "Lingua", "言語" } },
            { "English", new[]{ "English", "Inglese", "英語" } },
            { "Italian", new[]{ "Italian", "Italiano", "イタリア語" } },
            { "Japanese", new[]{ "Japanese", "Giapponese", "日本語" } },
            { "About ArkBoard", new[]{ "About ArkBoard", "Informazioni su ArkBoard", "ArkBoardについて" } },
            { "Add Text", new[]{ "Add Text", "Aggiungi testo", "テキストを追加" } },
            { "Flip Horizontally", new[]{ "Flip Horizontally", "Rifletti orizzontalmente", "左右反転" } },
            { "Flip Vertically", new[]{ "Flip Vertically", "Rifletti verticalmente", "上下反転" } },
            { "Rotate 90°", new[]{ "Rotate 90°", "Ruota di 90°", "90°回転" } },
            { "QUICK CONTROLS", new[]{ "QUICK CONTROLS", "COMANDI RAPIDI", "クイック操作" } },
            { "SELECTION", new[]{ "SELECTION", "SELEZIONE", "選択" } },
            { "Select an image to edit it.", new[]{ "Select an image to edit it.", "Seleziona un'immagine per modificarla.", "編集する画像を選択してください。" } },
            { "Rotation · degrees", new[]{ "Rotation · degrees", "Rotazione · gradi", "回転 · 度" } },
            { "Scale · % of original", new[]{ "Scale · % of original", "Scala · % dell'originale", "スケール · 元画像の%" } },
            { "Reset", new[]{ "Reset", "Reimposta", "リセット" } },
            { "Flip", new[]{ "Flip", "Rifletti", "反転" } },
            { "Remove Mask", new[]{ "Remove Mask", "Rimuovi maschera", "マスクを解除" } },
            { "Bring to Front", new[]{ "Bring to Front", "Porta in primo piano", "最前面へ" } },
            { "Send to Back", new[]{ "Send to Back", "Porta sul fondo", "最背面へ" } },
            { "LAYERS", new[]{ "LAYERS", "LIVELLI", "レイヤー" } },
            { "Opacity", new[]{ "Opacity", "Opacità", "不透明度" } },
            { "Close", new[]{ "Close", "Chiudi", "閉じる" } },
            { "Don't Save", new[]{ "Don't Save", "Non salvare", "保存しない" } },
            { "Cancel", new[]{ "Cancel", "Annulla", "キャンセル" } },
            { "Save changes to this project?", new[]{ "Save changes to this project?", "Salvare le modifiche a questo progetto?", "このプロジェクトへの変更を保存しますか？" } },
            { "Compatible files", new[]{ "Compatible files", "File compatibili", "対応ファイル" } },
            { "Platforms", new[]{ "Platforms", "Piattaforme", "対応OS" } },
            { "License", new[]{ "License", "Licenza", "ライセンス" } },
            { "Source and releases", new[]{ "Source and releases", "Codice sorgente e versioni", "ソースとリリース" } },
            { "Images", new[]{ "Images", "Immagini", "画像" } },
            { "Texts", new[]{ "Texts", "Testi", "テキスト" } },
            { "Selected", new[]{ "Selected", "Selezionati", "選択中" } },
            { "Text", new[]{ "Text", "Testo", "テキスト" } },
            { "Image", new[]{ "Image", "Immagine", "画像" } },
            { "objects", new[]{ "objects", "oggetti", "個のオブジェクト" } },
            { "Transforms apply to the selection", new[]{ "Transforms apply to the selection", "Le trasformazioni si applicano alla selezione", "変形は選択範囲に適用されます" } },
            { "Masked", new[]{ "Masked", "Mascherata", "マスク済み" } },
            { "layers", new[]{ "layers", "livelli", "レイヤー" } },
            { "visible", new[]{ "visible", " visibile", " 表示" } },
            { "hidden", new[]{ "hidden", " nascosto", " 非表示" } },
            { "Ready · Drop an image to get started", new[]{ "Ready · Drop an image to get started", "Pronto · Trascina un'immagine per iniziare", "準備完了 · 画像をドロップしてください" } },
            { "A space for your ideas", new[]{ "A space for your ideas", "Uno spazio per le tue idee", "アイデアのための空間" } },
            { "Drop images here from your computer or browser", new[]{ "Drop images here from your computer or browser", "Trascina qui immagini dal computer o dal browser", "PCやブラウザから画像をドロップ" } },
            { "or press Ctrl+I to import and Ctrl+V to paste", new[]{ "or press Ctrl+I to import and Ctrl+V to paste", "oppure usa Ctrl+I per importare e Ctrl+V per incollare", "Ctrl+Iで読み込み、Ctrl+Vで貼り付け" } }
            ,{ "Zoom at cursor", new[]{ "Zoom at cursor", "Zoom sul cursore", "カーソル位置でズーム" } }
            ,{ "Pan canvas", new[]{ "Pan canvas", "Sposta canvas", "キャンバスを移動" } }
            ,{ "Vertical drag zoom", new[]{ "Vertical drag zoom", "Zoom con trascinamento verticale", "上下ドラッグでズーム" } }
            ,{ "Multi-select", new[]{ "Multi-select", "Selezione multipla", "複数選択" } }
            ,{ "Select", new[]{ "Select", "Seleziona", "選択" } }
            ,{ "Proportional resize", new[]{ "Proportional resize", "Ridimensiona proporzionalmente", "縦横比を維持して拡縮" } }
            ,{ "Mask", new[]{ "Mask", "Maschera", "マスク" } }
            ,{ "Adjust mask", new[]{ "Adjust mask", "Regola maschera", "マスクを調整" } }
            ,{ "Move mask", new[]{ "Move mask", "Sposta maschera", "マスクを移動" } }
            ,{ "Edit text", new[]{ "Edit text", "Modifica testo", "テキストを編集" } }
            ,{ "Rotate images", new[]{ "Rotate images", "Ruota immagini", "画像を回転" } }
            ,{ "Mask controls / angle snap", new[]{ "Mask controls / angle snap", "Controlli maschera / aggancio angolo", "マスク操作 / 角度スナップ" } }
            ,{ "Reset scale / rotation / mask", new[]{ "Reset scale / rotation / mask", "Reimposta scala / rotazione / maschera", "スケール / 回転 / マスクをリセット" } }
            ,{ "Normalize / pack", new[]{ "Normalize / pack", "Uniforma / compatta", "サイズ統一 / 整列" } }
            ,{ "Select / deselect all", new[]{ "Select / deselect all", "Seleziona / deseleziona tutto", "すべて選択 / 解除" } }
            ,{ "Fit all", new[]{ "Fit all", "Adatta tutto", "全体表示" } }
            ,{ "Text tool · Drag to choose font size, release, then type · Esc to exit", new[]{ "Text tool · Drag to choose font size, release, then type · Esc to exit", "Testo · Trascina per scegliere la dimensione, rilascia e scrivi · Esc per uscire", "テキスト · ドラッグでサイズを決め、離して入力 · Escで終了" } }
            ,{ "Type text · Enter: new line · Esc or click outside: confirm", new[]{ "Type text · Enter: new line · Esc or click outside: confirm", "Scrivi · Invio: nuova riga · Esc o click esterno: conferma", "入力中 · Enter: 改行 · Escまたは外側クリック: 確定" } }
            ,{ "Text tool closed", new[]{ "Text tool closed", "Strumento testo chiuso", "テキストツールを終了" } }
            ,{ "Text confirmed · Drag to move · Double-click to edit", new[]{ "Text confirmed · Drag to move · Double-click to edit", "Testo confermato · Trascina per spostare · Doppio click per modificare", "テキスト確定 · ドラッグで移動 · ダブルクリックで編集" } }
            ,{ "Enter a valid numeric angle.", new[]{ "Enter a valid numeric angle.", "Inserisci un angolo numerico valido.", "有効な角度を入力してください。" } }
            ,{ "Scale must be between 0.1% and 10,000%.", new[]{ "Scale must be between 0.1% and 10,000%.", "La scala deve essere tra 0,1% e 10.000%.", "スケールは0.1%から10,000%の範囲で指定してください。" } }
            ,{ "Mask removed", new[]{ "Mask removed", "Maschera rimossa", "マスクを解除しました" } }
            ,{ "Masks removed", new[]{ "Masks removed", "Maschere rimosse", "マスクを解除しました" } }
            ,{ "Select at least two images to normalize their size.", new[]{ "Select at least two images to normalize their size.", "Seleziona almeno due immagini per uniformarne le dimensioni.", "サイズを揃える画像を2つ以上選択してください。" } }
            ,{ "Size normalized · Aspect ratios preserved", new[]{ "Size normalized · Aspect ratios preserved", "Dimensioni uniformate · Proporzioni mantenute", "サイズを統一 · 縦横比を維持" } }
            ,{ "Images packed · Sizes and rotations preserved", new[]{ "Images packed · Sizes and rotations preserved", "Immagini compattate · Dimensioni e rotazioni mantenute", "画像を整列 · サイズと回転を維持" } }
            ,{ "Project opened · All images are embedded", new[]{ "Project opened · All images are embedded", "Progetto aperto · Tutte le immagini sono incorporate", "プロジェクトを開きました · 全画像を埋め込み済み" } }
            ,{ "Saved · Images embedded in the project", new[]{ "Saved · Images embedded in the project", "Salvato · Immagini incorporate nel progetto", "保存しました · 画像はプロジェクトに埋め込み済み" } }
            ,{ "Image pasted", new[]{ "Image pasted", "Immagine incollata", "画像を貼り付けました" } }
            ,{ "Masked image pasted", new[]{ "Masked image pasted", "Immagine mascherata incollata", "マスク画像を貼り付けました" } }
            ,{ "Text copied", new[]{ "Text copied", "Testo copiato", "テキストをコピーしました" } }
            ,{ "Board locked · Clicks pass through to the application below", new[]{ "Board locked · Clicks pass through to the application below", "Board bloccata · I click passano all'applicazione sottostante", "ボードをロック · クリックは背後のアプリに届きます" } }
            ,{ "Board unlocked", new[]{ "Board unlocked", "Board sbloccata", "ボードのロックを解除" } }
            ,{ "Fit all / selection", new[]{ "Fit all / selection", "Adatta tutto / selezione", "全体 / 選択範囲を表示" } }
            ,{ "New / open / save", new[]{ "New / open / save", "Nuovo / apri / salva", "新規 / 開く / 保存" } }
            ,{ "Save as", new[]{ "Save as", "Salva con nome", "名前を付けて保存" } }
            ,{ "Import / paste", new[]{ "Import / paste", "Importa / incolla", "読み込み / 貼り付け" } }
            ,{ "Add text", new[]{ "Add text", "Aggiungi testo", "テキストを追加" } }
            ,{ "Copy / duplicate / delete", new[]{ "Copy / duplicate / delete", "Copia / duplica / elimina", "コピー / 複製 / 削除" } }
            ,{ "Undo / redo", new[]{ "Undo / redo", "Annulla / ripeti", "元に戻す / やり直す" } }
            ,{ "Flip horizontal / vertical", new[]{ "Flip horizontal / vertical", "Rifletti orizzontale / verticale", "左右 / 上下反転" } }
            ,{ "Rotate ±15°", new[]{ "Rotate ±15°", "Ruota ±15°", "±15°回転" } }
            ,{ "Move 1 / 10 units", new[]{ "Move 1 / 10 units", "Sposta di 1 / 10 unità", "1 / 10単位移動" } }
            ,{ "Zoom 100 / in / out", new[]{ "Zoom 100 / in / out", "Zoom 100 / avanti / indietro", "100% / 拡大 / 縮小" } }
            ,{ "Front / back", new[]{ "Front / back", "Primo piano / fondo", "前面 / 背面" } }
            ,{ "Clear selection / confirm text", new[]{ "Clear selection / confirm text", "Deseleziona / conferma testo", "選択解除 / テキスト確定" } }
        };

        internal static string T(string english)
        {
            string[] value;
            return Values.TryGetValue(english, out value) ? value[(int)Current] : english;
        }
    }
}
