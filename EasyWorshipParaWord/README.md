# EasyWorship ⇄ Word

Aplicação Windows (WinForms, .NET 8) com dois sentidos:

1. **Bases de dados → Word:** recebe o `Songs.db` e o `SongWords.db` do EasyWorship 7 e exporta todas as músicas para um `.docx`.
2. **Word → EasyWorship:** lê o `.docx` depois de o editares e cria um `.txt` por música (só as novas ou alteradas, se quiseres) para importares no EasyWorship.

Os ficheiros originais **não são alterados**: a aplicação trabalha sobre cópias temporárias
(por isso funciona mesmo com o EasyWorship aberto).

## Como usar

Arrasta o `Songs.db` e o `SongWords.db` para a janela (ou usa **Procurar…**).
Estão normalmente em `C:\Users\Public\Documents\Softouch\Easyworship\Default\Databases\Data`.

### Separador "Bases de dados → Word"
Escolhe o título do documento e se cada música começa numa página nova, e clica **Exportar para Word…**.

### Separador "Word → EasyWorship"
1. Escolhe o `.docx` editado (também podes arrastá-lo para a janela).
2. Escolhe a pasta onde guardar os `.txt` (por defeito, `ParaEasyWorship` ao lado do documento).
3. Com a opção **Só as músicas novas ou alteradas** ligada, a aplicação compara com o `Songs.db`/`SongWords.db`
   e só cria ficheiros para o que mudou. Indica também as músicas das bases de dados que deixaram de aparecer no documento.
4. No EasyWorship 7: crie/abra a música, escreve o título, clica no ícone **Importar** do editor de letras e escolhe o `.txt`.
   O EasyWorship importa **um documento de cada vez** e não documenta um formato de importação em massa,
   por isso **testa primeiro com uma música**.

## Estrutura do documento Word (mantém-na ao editar)

- Cada música começa com um **Título 1** (o painel de navegação do Word serve de índice).
- A seguir, opcionalmente, uma linha cinzenta com autor/copyright (**não** é importada para o EasyWorship).
- Cada slide tem a etiqueta (Verse, Chorus, End…) numa linha **a negrito**, e a letra por baixo, uma linha por parágrafo.
- Um **parágrafo vazio** dentro de uma música começa um novo slide (útil para slides sem etiqueta).
- Para uma música nova: Título 1 com o nome, etiquetas a negrito e letra por baixo.

Formato de cada `.txt` criado (UTF-8 com BOM, quebras de linha do Windows):

```
Verse 1
Primeira linha
Segunda linha

Chorus
...
```

## Sem Visual Studio (duplo clique)

1. Extrai o `.zip` todo para uma pasta.
2. Abre o **`Executar.bat`** (duplo clique).
3. Se o computador não tiver o .NET SDK 8, o ficheiro oferece instalá-lo com o `winget` ou abre a página de download.
   Depois de instalar, fecha a janela e abre o `Executar.bat` outra vez.
4. A primeira execução compila a aplicação e precisa de internet para descarregar um pacote; as seguintes são rápidas.

## Compilar

Requisitos: Windows, [.NET 8 SDK](https://dotnet.microsoft.com/download) (ou Visual Studio 2022).

```
dotnet run
```

Para gerar um executável:

```
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

(fica em `bin\Release\net8.0-windows\win-x64\publish\`; precisa do .NET 8 Desktop Runtime instalado).

## Notas

- A única dependência é o pacote NuGet `Microsoft.Data.Sqlite`, restaurado automaticamente. O `.docx` é lido e escrito sem bibliotecas externas.
- No fim da leitura das bases de dados a aplicação avisa das músicas em que o número de slides lidos não bate com o registado
  no EasyWorship; convém rever essas à mão.

## Ficheiros

| Ficheiro | O que faz |
|---|---|
| `SongReader.cs` | Lê as duas bases SQLite e junta título/autor com a letra |
| `RtfParser.cs` | Converte o RTF das letras em slides de texto simples (formato novo e antigo) |
| `DocxExporter.cs` | Gera o `.docx` |
| `DocxImporter.cs` | Lê o `.docx` editado e extrai músicas, etiquetas e letras |
| `SongDiff.cs` | Compara o Word editado com as bases de dados (novas / alteradas / iguais) |
| `SongTextExporter.cs` | Escreve um `.txt` por música |
| `MainForm.cs` | Janela, arrastar e largar, separadores |
