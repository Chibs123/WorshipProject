using System.Diagnostics;

namespace EasyWorshipParaWord;

public sealed class MainForm : Form
{
    // ---- Bases de dados (partilhadas pelos dois separadores) ----
    private readonly TextBox _songsBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _wordsBox = new() { ReadOnly = true, Dock = DockStyle.Fill };

    // ---- Separador 1: bases de dados → Word ----
    private readonly TextBox _titleBox = new() { Text = "Cancioneiro", Dock = DockStyle.Fill };
    private readonly CheckBox _pageBreak = new() { Text = "Começar cada música numa página nova", AutoSize = true };
    private readonly Button _exportWord = new() { Text = "Exportar para Word…", AutoSize = true, Enabled = false };

    // ---- Separador 2: Word → EasyWorship ----
    private readonly TextBox _docxBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _outBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly CheckBox _changedOnly = new()
    {
        Text = "Só as músicas novas ou alteradas (compara com o Songs.db e o SongWords.db acima)",
        AutoSize = true,
        Checked = true,
        Enabled = false
    };
    private readonly Button _exportText = new() { Text = "Criar ficheiros para o EasyWorship…", AutoSize = true, Enabled = false };

    private readonly ProgressBar _progress = new() { Style = ProgressBarStyle.Marquee, Visible = false, Dock = DockStyle.Bottom, Height = 6 };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Bottom, Height = 150 };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    private string? _songsPath;
    private string? _wordsPath;
    private string? _docxPath;
    private string? _outFolder;
    private bool _busy;

    public MainForm()
    {
        Text = "EasyWorship ⇄ Word";
        ClientSize = new Size(780, 560);
        MinimumSize = new Size(640, 520);
        StartPosition = FormStartPosition.CenterScreen;

        // Zona de cima: as duas bases de dados.
        var top = NewTable(rows: 3);
        top.Dock = DockStyle.Top;
        top.AutoSize = true;
        var browseSongs = new Button { Text = "Procurar…", AutoSize = true };
        browseSongs.Click += (_, _) => BrowseDb(isWords: false);
        var browseWords = new Button { Text = "Procurar…", AutoSize = true };
        browseWords.Click += (_, _) => BrowseDb(isWords: true);
        AddRow(top, 0, "Songs.db", _songsBox, browseSongs);
        AddRow(top, 1, "SongWords.db", _wordsBox, browseWords);
        var hint = new Label
        {
            Text = "Arrasta o Songs.db e o SongWords.db (e o .docx) para esta janela. " +
                   "Ficam normalmente em ...\\Softouch\\Easyworship\\Default\\Databases\\Data. Os originais não são alterados.",
            AutoSize = true,
            MaximumSize = new Size(740, 0),
            Margin = new Padding(3, 4, 3, 4)
        };
        top.Controls.Add(hint, 0, 2);
        top.SetColumnSpan(hint, 3);

        // Separador 1.
        var tab1 = new TabPage("Bases de dados → Word");
        var t1 = NewTable(rows: 4);
        AddRow(t1, 0, "Título do documento", _titleBox, null);
        t1.Controls.Add(_pageBreak, 0, 1);
        t1.SetColumnSpan(_pageBreak, 3);
        _exportWord.Click += async (_, _) => await ExportWordAsync();
        t1.Controls.Add(_exportWord, 0, 2);
        t1.SetColumnSpan(_exportWord, 3);
        tab1.Controls.Add(t1);

        // Separador 2.
        var tab2 = new TabPage("Word → EasyWorship");
        var t2 = NewTable(rows: 6);
        var browseDocx = new Button { Text = "Procurar…", AutoSize = true };
        browseDocx.Click += (_, _) => BrowseDocx();
        var browseOut = new Button { Text = "Escolher…", AutoSize = true };
        browseOut.Click += (_, _) => BrowseOutFolder();
        AddRow(t2, 0, "Documento Word", _docxBox, browseDocx);
        AddRow(t2, 1, "Guardar ficheiros em", _outBox, browseOut);
        t2.Controls.Add(_changedOnly, 0, 2);
        t2.SetColumnSpan(_changedOnly, 3);
        var how = new Label
        {
            Text = "É criado um .txt por música. No EasyWorship: Músicas → nova música (ou abre a existente), " +
                   "escreve o título, clica no ícone Importar do editor e escolhe o .txt. " +
                   "Testa primeiro com uma música. O autor/copyright do Word não é importado.",
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            Margin = new Padding(3, 8, 3, 8)
        };
        t2.Controls.Add(how, 0, 3);
        t2.SetColumnSpan(how, 3);
        _exportText.Click += async (_, _) => await ExportTextAsync();
        t2.Controls.Add(_exportText, 0, 4);
        t2.SetColumnSpan(_exportText, 3);
        tab2.Controls.Add(t2);

        _tabs.TabPages.Add(tab1);
        _tabs.TabPages.Add(tab2);

        // A ordem importa no docking: o controlo que preenche vai primeiro.
        Controls.Add(_tabs);
        Controls.Add(_log);
        Controls.Add(_progress);
        Controls.Add(top);

        HookDrop(this);
        Log("Escolhe (ou arrasta para aqui) o Songs.db e o SongWords.db.");
    }

    private static TableLayoutPanel NewTable(int rows)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = rows,
            Padding = new Padding(8)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < rows; i++) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return table;
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control box, Control? button)
    {
        var lbl = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft };
        table.Controls.Add(lbl, 0, row);
        table.Controls.Add(box, 1, row);
        if (button == null) table.SetColumnSpan(box, 2);
        else table.Controls.Add(button, 2, row);
    }

    // ---- Arrastar e largar ----

    private void HookDrop(Control c)
    {
        c.AllowDrop = true;
        c.DragEnter += OnDragEnter;
        c.DragDrop += OnDragDrop;
        foreach (Control child in c.Controls) HookDrop(child);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (string f in files)
        {
            string ext = Path.GetExtension(f);
            string name = Path.GetFileNameWithoutExtension(f);
            if (ext.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                SetDocx(f);
                _tabs.SelectedIndex = 1;
            }
            else if (name.Contains("words", StringComparison.OrdinalIgnoreCase)) _wordsPath = f;
            else if (name.Contains("song", StringComparison.OrdinalIgnoreCase)) _songsPath = f;
            else Log($"Ficheiro ignorado (nome não reconhecido): {Path.GetFileName(f)}");
        }
        UpdateState();
    }

    // ---- Escolher ficheiros ----

    private void BrowseDb(bool isWords)
    {
        using var dlg = new OpenFileDialog
        {
            Title = isWords ? "Escolhe o SongWords.db" : "Escolhe o Songs.db",
            Filter = "Bases de dados (*.db)|*.db|Todos os ficheiros|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (isWords) _wordsPath = dlg.FileName; else _songsPath = dlg.FileName;

        // Se o outro ficheiro estiver na mesma pasta com o nome habitual, preenche-o.
        string? dir = Path.GetDirectoryName(dlg.FileName);
        if (dir != null)
        {
            string sibling = Path.Combine(dir, isWords ? "Songs.db" : "SongWords.db");
            if (File.Exists(sibling))
            {
                if (isWords && _songsPath == null) _songsPath = sibling;
                if (!isWords && _wordsPath == null) _wordsPath = sibling;
            }
        }
        UpdateState();
    }

    private void BrowseDocx()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Escolhe o documento Word editado",
            Filter = "Documento Word (*.docx)|*.docx",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        SetDocx(dlg.FileName);
        UpdateState();
    }

    private void SetDocx(string path)
    {
        _docxPath = path;
        string? dir = Path.GetDirectoryName(path);
        if (_outFolder == null && dir != null) _outFolder = Path.Combine(dir, "ParaEasyWorship");
    }

    private void BrowseOutFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Pasta onde guardar os ficheiros .txt" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _outFolder = dlg.SelectedPath;
        UpdateState();
    }

    private void UpdateState()
    {
        bool dbs = _songsPath != null && _wordsPath != null;
        _songsBox.Text = _songsPath ?? "";
        _wordsBox.Text = _wordsPath ?? "";
        _docxBox.Text = _docxPath ?? "";
        _outBox.Text = _outFolder ?? "";
        _exportWord.Enabled = !_busy && dbs;
        _changedOnly.Enabled = !_busy && dbs;
        _exportText.Enabled = !_busy && _docxPath != null && _outFolder != null;
    }

    // ---- Separador 1: exportar para Word ----

    private async Task ExportWordAsync()
    {
        if (_songsPath == null || _wordsPath == null) return;

        using var dlg = new SaveFileDialog
        {
            Title = "Guardar documento Word",
            Filter = "Documento Word (*.docx)|*.docx",
            FileName = "Cancioneiro.docx",
            OverwritePrompt = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        string songsPath = _songsPath, wordsPath = _wordsPath, outPath = dlg.FileName;
        string title = _titleBox.Text;
        bool pageBreak = _pageBreak.Checked;

        SetBusy(true);
        try
        {
            Log("A ler as bases de dados...");
            ReadResult result = await Task.Run(() => SongReader.Read(songsPath, wordsPath));
            int slides = result.Songs.Sum(s => s.Slides.Count);
            Log($"Lidas {result.Songs.Count} músicas ({slides} slides).");

            if (result.Songs.Count == 0)
            {
                Log("Não há músicas nestas bases de dados — confirma que escolheste os ficheiros certos.");
                MessageBox.Show(this, "As bases de dados escolhidas não têm músicas.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LogWarnings(result.Warnings);

            Log("A criar o documento Word...");
            await Task.Run(() => DocxExporter.Export(result.Songs, outPath, title, pageBreak));
            Log("Concluído: " + outPath);

            if (MessageBox.Show(this, "Documento criado. Queres abri-lo agora?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ---- Separador 2: Word → ficheiros para o EasyWorship ----

    private async Task ExportTextAsync()
    {
        if (_docxPath == null || _outFolder == null) return;

        string docx = _docxPath, folder = _outFolder;
        string? songsDb = _songsPath, wordsDb = _wordsPath;
        bool compare = _changedOnly.Checked && songsDb != null && wordsDb != null;

        SetBusy(true);
        try
        {
            Log("A ler o documento Word...");
            var outcome = await Task.Run(() =>
            {
                List<Song> edited = DocxImporter.Read(docx);
                DiffResult? diff = null;
                List<Song> toWrite = edited;
                if (compare)
                {
                    ReadResult original = SongReader.Read(songsDb!, wordsDb!);
                    diff = SongDiff.Compare(edited, original.Songs);
                    toWrite = diff.ToExport;
                }
                int written = SongTextExporter.Export(toWrite, folder);
                return (Total: edited.Count, Diff: diff, Written: written);
            });

            if (outcome.Total == 0)
            {
                Log("Não encontrei músicas no documento (cada música tem de ter um título no estilo Título 1).");
                MessageBox.Show(this, "Não encontrei músicas neste documento.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Log($"O documento tem {outcome.Total} músicas.");
            if (outcome.Diff != null)
            {
                Log($"Alteradas: {outcome.Diff.Changed} · Novas: {outcome.Diff.Added} · Iguais às bases de dados: {outcome.Diff.Unchanged}.");
                if (outcome.Diff.MissingInDocument.Count > 0)
                {
                    Log($"{outcome.Diff.MissingInDocument.Count} música(s) das bases de dados não aparecem no documento (título mudado ou apagado):");
                    foreach (string t in outcome.Diff.MissingInDocument.Take(15)) Log("  • " + t);
                    if (outcome.Diff.MissingInDocument.Count > 15) Log($"  … e mais {outcome.Diff.MissingInDocument.Count - 15}.");
                }
            }
            Log($"Criados {outcome.Written} ficheiro(s) .txt em: {folder}");

            if (outcome.Written > 0 &&
                MessageBox.Show(this, $"Criados {outcome.Written} ficheiro(s). Queres abrir a pasta?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ---- Utilitários ----

    private void LogWarnings(List<string> warnings)
    {
        if (warnings.Count == 0) return;
        Log($"{warnings.Count} aviso(s):");
        foreach (string w in warnings.Take(20)) Log("  • " + w);
        if (warnings.Count > 20) Log($"  … e mais {warnings.Count - 20}.");
    }

    private void ShowError(Exception ex)
    {
        Log("Erro: " + ex.Message);
        MessageBox.Show(this, ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _progress.Visible = busy;
        UpdateState();
    }

    private void Log(string message) => _log.AppendText(message + Environment.NewLine);
}
