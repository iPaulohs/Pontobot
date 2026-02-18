using Microsoft.Playwright;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    private static ITelegramBotClient? bot;
    private static IPlaywright? playwright;
    private static IBrowser? browser;

    static async Task Main(string[] args)
    {
        const string telegramToken = "8544035387:AAFl8woWwEUt2T5TNZxsZJSvs4-wbcWusdc";
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

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        if (update.Type == Telegram.Bot.Types.Enums.UpdateType.CallbackQuery)
        {
            var callbackQuery = update.CallbackQuery!;

            switch (callbackQuery.Data)
            {
                case "exception_yes":
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "Processando exceção...",
                        cancellationToken: cancellationToken);
                    await botClient.SendMessage(callbackQuery.Message!.Chat.Id,
                        "Confirmado. Registrando ponto por exceção...", cancellationToken: cancellationToken);

                    await RegistrarPonto("paulo.santos@rumosolucoes.com", "OvgBmcEM", cancellationToken);

                    await botClient.SendMessage(callbackQuery.Message.Chat.Id, "✅ Ponto (Exceção) registrado com sucesso!",
                        cancellationToken: cancellationToken);
                    break;
                case "exception_no":
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "Ação cancelada.",
                        cancellationToken: cancellationToken);
                    await botClient.SendMessage(callbackQuery.Message!.Chat.Id, "Ok, nada foi feito.",
                        cancellationToken: cancellationToken);
                    break;
            }

            await botClient.EditMessageReplyMarkup(
                chatId: callbackQuery.Message!.Chat.Id,
                messageId: callbackQuery.Message.MessageId,
                replyMarkup: null,
                cancellationToken: cancellationToken);

            return;
        }

        if (update.Message is { Text: "BaterPontoAgora" } message)
        {
            var brZone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
            var brTime = TimeZoneInfo.ConvertTimeFromUtc(message.Date, brZone);
            var messageTime = brTime.TimeOfDay;

            var isInRange =
                (messageTime >= new TimeSpan(8, 55, 0) && messageTime <= new TimeSpan(9, 5, 0)) ||
                (messageTime >= new TimeSpan(12, 25, 0) && messageTime <= new TimeSpan(12, 35, 0)) ||
                (messageTime >= new TimeSpan(13, 25, 0) && messageTime <= new TimeSpan(13, 35, 0)) ||
                (messageTime >= new TimeSpan(17, 55, 0));

            if (isInRange)
            {
                await RegistrarPonto("paulo.santos@rumosolucoes.com", "OvgBmcEM", cancellationToken);
                await botClient.SendMessage(message.Chat.Id, "✅ Ponto registrado com sucesso!",
                    cancellationToken: cancellationToken);
            }
            else
            {
                var inlineKeyboard = new InlineKeyboardMarkup
                ([
                    [
                        InlineKeyboardButton.WithCallbackData("Sim", "exception_yes"),
                        InlineKeyboardButton.WithCallbackData("Não", "exception_no"),
                    ]
                ]);

                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"⚠️ Solicitação fora do horário ({brTime:HH:mm}). Deseja registrar uma exceção?",
                    replyMarkup: inlineKeyboard,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine("Erro no bot:");
        Console.WriteLine(exception);
        return Task.CompletedTask;
    }

    private static async Task RegistrarPonto(string email, string senha, CancellationToken cancellationToken)
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

        Console.WriteLine("Ponto registrado com sucesso 🚀");
    }
}