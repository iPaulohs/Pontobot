using Microsoft.Playwright;
using Telegram.Bot;
using Telegram.Bot.Types;

class Program
{
    private static ITelegramBotClient? bot;
    private static IPlaywright? playwright;
    private static IBrowser? browser;

    static async Task Main(string[] args)
    {
        var telegramToken = "8544035387:AAFl8woWwEUt2T5TNZxsZJSvs4-wbcWusdc";
        var email = "paulo.santos@rumosolucoes.com";
        var senha = "OvgBmcEM";

        bot = new TelegramBotClient(telegramToken);

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
            if (browser != null)
                await browser.CloseAsync();

            playwright?.Dispose();
        }
    }

    static async Task InicializarBrowser()
    {
        playwright = await Playwright.CreateAsync();

        browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = false,
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

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is { Text: "BaterPontoAgora" } message)
        {
            Console.WriteLine("------------");
            Console.WriteLine($"Usuário: {message.From?.Username}");
            Console.WriteLine($"Texto: {message.Text}");
            Console.WriteLine("------------");

            try
            {
                await RegistrarPonto(
                    "paulo.santos@rumosolucoes.com",
                    "OvgBmcEM",
                    cancellationToken);

                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "✅ Ponto registrado com sucesso!",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro: " + ex);

                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "❌ Houve um problema ao registrar o ponto.",
                    cancellationToken: cancellationToken);
            }
        }
        else
        {
            await botClient.SendMessage(
                chatId: update.Message!.Chat.Id,
                text: "Você não é bem-vindo aqui e não vai conseguir fazer nada. Saia!",
                cancellationToken: cancellationToken);
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine("Erro no bot:");
        Console.WriteLine(exception);
        return Task.CompletedTask;
    }

    static async Task RegistrarPonto(string email, string senha, CancellationToken cancellationToken)
    {
        await using var context = await browser!.NewContextAsync();
        var page = await context.NewPageAsync();

        page.SetDefaultTimeout(30000);

        Console.WriteLine("Acessando página de login...");

        await page.GotoAsync(
            "https://genial.rumosolucoes.com/Rumo/Acesso/Acesso/Login",
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

        await page.FillAsync("input[name=\"Email\"]", email);
        await page.FillAsync("input[name=\"Senha\"]", senha);

        await Task.WhenAll(
            page.WaitForNavigationAsync(),
            page.ClickAsync("#btnLogin")
        );

        Console.WriteLine("Login realizado.");

        var botaoRegistrar = page.Locator("#botaoRegistrarPonto");

        await botaoRegistrar.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible
        });

        await botaoRegistrar.ClickAsync();

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await page.WaitForTimeoutAsync(1000);

        var modal = page.Locator("#modalRegistroPonto");

        if (await modal.IsVisibleAsync())
        {
            Console.WriteLine("Modal detectado, salvando...");

            var botaoSalvar = modal.Locator("button:has-text(\"Salvar\")");

            await Task.WhenAll(
                page.WaitForNavigationAsync(new()
                {
                    UrlString = "**/Configuracao/LancamentoHoras"
                }),
                botaoSalvar.ClickAsync()
            );

            Console.WriteLine("Redirecionado para LancamentoHoras.");
        }

        var screenshotBytes = await page.ScreenshotAsync(new()
        {
            FullPage = true
        });

        using var stream = new MemoryStream(screenshotBytes);

        await bot!.SendPhoto(
            chatId: 1029116793,
            photo: InputFile.FromStream(stream, "comprovante.png"),
            caption: "📸 Comprovante de registro de ponto",
            cancellationToken: cancellationToken
        );

        Console.WriteLine("Ponto registrado com sucesso 🚀");
    }
}
