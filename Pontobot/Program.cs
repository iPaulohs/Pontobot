using Microsoft.Playwright;
using Telegram.Bot;
using Telegram.Bot.Types;

class Program
{
    private static readonly ITelegramBotClient bot = new TelegramBotClient("8544035387:AAFl8woWwEUt2T5TNZxsZJSvs4-wbcWusdc");
    private static IPlaywright? playwright;
    private static IBrowser? browser;

    static async Task Main(string[] args)
    {
        await InicializarBrowser();

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("Encerrando bot...");
            cts.Cancel();
            e.Cancel = true;
        };

        bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            cancellationToken: cts.Token);

        Console.WriteLine("Bot ouvindo mensagens...");

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Aplicação encerrada.");
        }
        finally
        {
            await browser!.CloseAsync();
            playwright!.Dispose();
        }
    }

    static async Task InicializarBrowser()
    {
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            Args =
            [
                "--no-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--disable-setuid-sandbox"
            ]
        });

        Console.WriteLine("Browser inicializado.");
    }

    static async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken)
    {
        if (update.Message is { Text: not null } message)
        {
            Console.WriteLine("------------");
            Console.WriteLine($"Usuário: {message.From?.Username}");
            Console.WriteLine($"Texto: {message.Text}");
            Console.WriteLine("------------");

            try
            {
                await RegistrarPonto();
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "✅ Ponto registrado com sucesso!",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro: " + ex.Message);
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "❌ Houve um problema ao registrar o ponto.",
                    cancellationToken: cancellationToken);
            }
        }
    }

    static Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("Erro no bot:");
        Console.WriteLine(exception);
        return Task.CompletedTask;
    }

    static async Task RegistrarPonto()
    {
        await using var context = await browser!.NewContextAsync();
        var page = await context.NewPageAsync();

        page.SetDefaultTimeout(30000);

        await page.GotoAsync(
            "https://genial.rumosolucoes.com/Rumo/Acesso/Acesso/Login",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await page.FillAsync("input[name=\"Email\"]", "paulo.santos@rumosolucoes.com");
        await page.FillAsync("input[name=\"Senha\"]", "OvgBmcEM");
        await page.ClickAsync("#btnLogin");

        var botaoRegistrar = page.Locator("#botaoRegistrarPonto");
        await botaoRegistrar.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await botaoRegistrar.ClickAsync();

        var modal = page.Locator("#modalRegistroPonto");
        await modal.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        var botaoCancelar = modal.Locator("button:has-text(\"Cancelar\")");
        await botaoCancelar.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await botaoCancelar.ClickAsync(new() { Force = true });

        var screenshotBytes = await page.ScreenshotAsync(new() { FullPage = true });
        using var stream = new MemoryStream(screenshotBytes);

        await bot.SendPhoto(
            chatId: 1029116793,
            photo: InputFile.FromStream(stream, "comprovante.png"),
            caption: "📸 Comprovante de registro de ponto"
        );

        Console.WriteLine("Ponto registrado com sucesso 🚀");
    }
}