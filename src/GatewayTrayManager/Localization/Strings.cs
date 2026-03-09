using System.Globalization;

namespace GatewayTrayManager.Localization;

/// <summary>
/// Provides localized strings for the application.
/// Supports English (default) and Italian.
/// </summary>
public static class Strings
{
    private static readonly bool IsItalian = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("it", StringComparison.OrdinalIgnoreCase);

    // === Application ===
    public static string AppName => "Gateway Tray Manager";

    // === Tray Menu ===
    public static string MenuService => IsItalian ? "🖥️ Servizio:" : "🖥️ Service:";
    public static string MenuGateway => IsItalian ? "🌐 Gateway:" : "🌐 Gateway:";
    public static string MenuPerformance => IsItalian ? "📊 Prestazioni:" : "📊 Performance:";
    public static string MenuStart => IsItalian ? "▶️ Avvia" : "▶️ Start";
    public static string MenuStop => IsItalian ? "⏹️ Arresta" : "⏹️ Stop";
    public static string MenuRestart => IsItalian ? "🔄 Riavvia" : "🔄 Restart";
    public static string MenuOpenGateway => IsItalian ? "🌍 Apri Gateway" : "🌍 Open Gateway";
    public static string MenuRefresh => IsItalian ? "🔃 Aggiorna ora" : "🔃 Refresh now";
    public static string MenuConfiguration => IsItalian ? "⚙️ Configurazione..." : "⚙️ Configuration...";
    public static string MenuExit => IsItalian ? "❌ Esci" : "❌ Exit";

    // === Tray Status ===
    public static string StatusLoading => IsItalian ? "(caricamento...)" : "(loading...)";
    public static string StatusOK => "OK";
    public static string StatusFail => IsItalian ? "ERRORE" : "FAIL";
    public static string StatusUnavailable => IsItalian ? "(non disponibile)" : "(unavailable)";
    public static string StatusNotAvailable => "(n/a)";
    public static string GatewayStatusChanged => IsItalian ? "Stato gateway cambiato" : "Gateway status changed";

    // === ConfigForm ===
    public static string ConfigTitle => IsItalian ? "Gateway Tray Manager - Configurazione" : "Gateway Tray Manager - Configuration";
    public static string ConfigServiceName => IsItalian ? "Nome Servizio:" : "Service Name:";
    public static string ConfigGatewayUrl => "Gateway URL:";
    public static string ConfigPollInterval => IsItalian ? "Intervallo Poll (ms):" : "Poll Interval (ms):";
    public static string ConfigHttpTimeout => IsItalian ? "Timeout HTTP (s):" : "HTTP Timeout (s):";
    public static string ConfigUsername => "Username:";
    public static string ConfigPassword => "Password:";
    public static string ConfigOptional => IsItalian ? "(opzionale)" : "(optional)";
    public static string ConfigAutoStart => IsItalian ? "🚀 Avvia automaticamente con Windows" : "🚀 Start automatically with Windows";
    public static string ConfigUseSessionAuth => IsItalian ? "🔐 Usa autenticazione sessione per metriche performance" : "🔐 Use session auth for performance metrics";
    public static string ConfigUseSessionAuthTooltip => IsItalian 
        ? "Abilita per leggere metriche CPU/memoria da /data/api/v1/systemPerformance/currentGauges (richiede credenziali)" 
        : "Enable to read CPU/memory metrics from /data/api/v1/systemPerformance/currentGauges (requires credentials)";
    public static string ConfigTestConnection => IsItalian ? "🔍 Test Connessione" : "🔍 Test Connection";
    public static string ConfigSave => IsItalian ? "💾 Salva" : "💾 Save";
    public static string ConfigCancel => IsItalian ? "Annulla" : "Cancel";

    // === Validation ===
    public static string ValidationError => IsItalian ? "Errore di Validazione" : "Validation Error";
    public static string ValidationServiceNameRequired => IsItalian ? "Il Nome Servizio è obbligatorio." : "Service Name is required.";
    public static string ValidationGatewayUrlRequired => IsItalian ? "L'URL del Gateway è obbligatorio." : "Gateway URL is required.";
    public static string ValidationGatewayUrlInvalid => IsItalian ? "L'URL del Gateway non è valido." : "Gateway URL is not a valid URL.";
    public static string ValidationSessionAuthCredentials => IsItalian 
        ? "L'autenticazione sessione richiede sia Username che Password." 
        : "Session authentication requires both Username and Password.";

    // === Save/Restart ===
    public static string SaveSuccess => IsItalian ? "Configurazione salvata con successo!" : "Configuration saved successfully!";
    public static string SaveAutoStartEnabled => IsItalian ? "\n\n✅ Avvio automatico con Windows: Abilitato" : "\n\n✅ Auto-start with Windows: Enabled";
    public static string SaveAutoStartDisabled => IsItalian ? "\n\n❌ Avvio automatico con Windows: Disabilitato" : "\n\n❌ Auto-start with Windows: Disabled";
    public static string SaveAuthConfigured => IsItalian ? "\n🔐 Autenticazione: Configurata" : "\n🔐 Authentication: Configured";
    public static string SaveSessionAuthEnabled => IsItalian ? "\n🔑 Auth Sessione: Abilitata (metriche performance)" : "\n🔑 Session Auth: Enabled (performance metrics)";
    public static string RestartRequired => IsItalian ? "Riavvio Richiesto" : "Restart Required";
    public static string RestartMessage => IsItalian 
        ? "Alcune impostazioni richiedono un riavvio per essere applicate.\n\nVuoi riavviare l'applicazione ora?" 
        : "Some settings require a restart to take effect.\n\nDo you want to restart the application now?";
    public static string RestartError => IsItalian ? "Errore Riavvio" : "Restart Error";
    public static string RestartErrorMessage => IsItalian 
        ? "Impossibile riavviare l'applicazione:\n{0}\n\nRiavvia manualmente." 
        : "Failed to restart application:\n{0}\n\nPlease restart manually.";
    public static string SaveError => IsItalian ? "Errore" : "Error";
    public static string SaveErrorMessage => IsItalian ? "Impossibile salvare la configurazione:\n{0}" : "Failed to save configuration:\n{0}";
    public static string Success => IsItalian ? "Successo" : "Success";

    // === Test Connection ===
    public static string TestConnectionTo => IsItalian ? "Test connessione a:" : "Testing connection to:";
    public static string TestStatusPing => IsItalian ? "[1] Test /StatusPing ..." : "[1] Testing /StatusPing ...";
    public static string TestStatusPingOK => IsItalian ? "    ✅ StatusPing OK" : "    ✅ StatusPing OK";
    public static string TestStatusPingFailed => IsItalian ? "    ❌ StatusPing FALLITO:" : "    ❌ StatusPing FAILED:";
    public static string TestGwinfo => IsItalian ? "[2] Test /system/gwinfo ..." : "[2] Testing /system/gwinfo ...";
    public static string TestGwinfoOK => IsItalian ? "    ✅ gwinfo OK" : "    ✅ gwinfo OK";
    public static string TestGwinfoFailed => IsItalian ? "    ❌ gwinfo FALLITO:" : "    ❌ gwinfo FAILED:";
    public static string TestService => IsItalian ? "[3] Test Servizio:" : "[3] Testing Service:";
    public static string TestServiceOK => IsItalian ? "    ✅ Servizio OK:" : "    ✅ Service OK:";
    public static string TestServiceFailed => IsItalian ? "    ❌ Servizio FALLITO:" : "    ❌ Service FAILED:";
    public static string TestPerformance => IsItalian ? "[4] Test Metriche Performance (Auth Sessione) ..." : "[4] Testing Performance Metrics (Session Auth) ...";
    public static string TestPerformanceSkipped => IsItalian ? "    ⚠️ Saltato: Username/Password richiesti" : "    ⚠️ Skipped: Username/Password required";
    public static string TestSessionAuthOK => IsItalian ? "    ✅ Auth Sessione OK" : "    ✅ Session Auth OK";
    public static string TestSessionAuthFailed => IsItalian ? "    ❌ Auth Sessione FALLITA:" : "    ❌ Session Auth FAILED:";
    public static string TestAllPassed => IsItalian ? "✅ Tutti i test superati! Configurazione valida." : "✅ All tests passed! Configuration is valid.";
    public static string TestSomeFailed => IsItalian ? "⚠️ Alcuni test falliti. Controlla la configurazione." : "⚠️ Some tests failed. Check your configuration.";
    public static string TestError => IsItalian ? "❌ Errore:" : "❌ Error:";
    public static string TestUnexpectedError => IsItalian ? "❌ Errore imprevisto:" : "❌ Unexpected error:";
    public static string TestResponse => IsItalian ? "    Risposta:" : "    Response:";
    public static string TestLoginFailed => IsItalian ? "Login fallito:" : "Login failed:";
    public static string TestPerfApiFailed => IsItalian ? "API Performance fallita:" : "Performance API failed:";
    public static string TestRawResponse => IsItalian ? "Risposta grezza:" : "Raw response:";

    // OIDC Authentication messages
    public static string TestAuthOidcRequired => IsItalian 
        ? "L'endpoint richiede autenticazione OIDC interattiva.\n    Il gateway è configurato per usare un Identity Provider esterno.\n    Verifica la configurazione dell'IdP sul gateway per abilitare Basic Auth." 
        : "The endpoint requires interactive OIDC authentication.\n    The gateway is configured to use an external Identity Provider.\n    Check the IdP configuration on the gateway to enable Basic Auth.";
    public static string TestAuthAllMethodsFailed => IsItalian 
        ? "Tutti i metodi di autenticazione falliti (Form Login + Basic Auth).\n    Probabilmente il gateway usa OIDC che richiede autenticazione interattiva." 
        : "All authentication methods failed (Form Login + Basic Auth).\n    The gateway likely uses OIDC which requires interactive authentication.";
    public static string TestAuthCheckCredentials => IsItalian 
        ? "Verifica username/password o la configurazione dell'Identity Provider." 
        : "Check username/password or the Identity Provider configuration.";

    // === Program.cs (Startup/Errors) ===
    public static string AppError => IsItalian ? "Errore Gateway Tray Manager" : "Gateway Tray Manager Error";
    public static string ErrorOccurred => IsItalian ? "Si è verificato un errore:\n{0}\n\nDettagli salvati in crash.log" : "An error occurred:\n{0}\n\nDetails logged to crash.log";
    public static string FatalErrorOccurred => IsItalian ? "Si è verificato un errore fatale:\n{0}\n\nDettagli salvati in crash.log" : "A fatal error occurred:\n{0}\n\nDetails logged to crash.log";
    public static string AppAlreadyRunning => IsItalian ? "Applicazione già in esecuzione" : "Application Already Running";
    public static string AppAlreadyRunningMessage => IsItalian 
        ? "Gateway Tray Manager è già in esecuzione.\n\nVuoi chiudere l'istanza esistente e avviarne una nuova?" 
        : "Gateway Tray Manager is already running.\n\nDo you want to close the existing instance and start a new one?";
    public static string CouldNotCloseInstance => IsItalian 
        ? "Impossibile chiudere l'istanza esistente.\nChiudila manualmente e riprova." 
        : "Could not close the existing instance.\nPlease close it manually and try again.";
    public static string FailedToStartApp => IsItalian ? "Impossibile avviare l'applicazione:\n{0}" : "Failed to start application:\n{0}";

    // === ServiceOperationForm ===
    public static string ServiceOperationTitle => IsItalian ? "Servizio Gateway - {0}" : "Gateway Service - {0}";
    public static string OperationStart => IsItalian ? "▶️ Avvio del servizio Gateway..." : "▶️ Starting Gateway Service...";
    public static string OperationStop => IsItalian ? "⏹️ Arresto del servizio Gateway..." : "⏹️ Stopping Gateway Service...";
    public static string OperationRestart => IsItalian ? "🔄 Riavvio del servizio Gateway..." : "🔄 Restarting Gateway Service...";
    public static string OperationProcessing => IsItalian ? "Elaborazione..." : "Processing...";
    public static string WaitingForGateway => IsItalian ? "🌐 In attesa che il Gateway sia pronto..." : "🌐 Waiting for Gateway to be ready...";
    public static string CheckingStatusPing => IsItalian ? "Controllo endpoint /StatusPing..." : "Checking /StatusPing endpoint...";
    public static string GatewayStarting => IsItalian ? "🌐 Gateway in avvio{0} - {1}s" : "🌐 Gateway starting{0} - {1}s";
    public static string ServiceUnavailable503 => IsItalian ? "Servizio non disponibile (503) - Il Gateway si sta inizializzando..." : "Service Unavailable (503) - Gateway is initializing...";
    public static string GatewayState => IsItalian ? "🌐 Stato Gateway: {0}{1} - {2}s" : "🌐 Gateway state: {0}{1} - {2}s";
    public static string WaitingForRunning => IsItalian ? "In attesa dello stato RUNNING..." : "Waiting for RUNNING state...";
    public static string GatewayIsRunning => IsItalian ? "✅ Il Gateway è RUNNING" : "✅ Gateway is RUNNING";
    public static string GatewayRunningStatusOK => IsItalian ? "Gateway in esecuzione | Stato OK" : "Gateway running | Status OK";
    public static string GatewayIsResponding => IsItalian ? "✅ Il Gateway risponde" : "✅ Gateway is responding";
    public static string WaitingForGatewayDots => IsItalian ? "🌐 In attesa del Gateway{0} - {1}s" : "🌐 Waiting for Gateway{0} - {1}s";
    public static string GatewayNotReadyYet => IsItalian ? "HTTP {0} - Il Gateway non è ancora pronto..." : "HTTP {0} - Gateway not ready yet...";
    public static string ConnectionRefused => IsItalian ? "Connessione rifiutata - Il Gateway non è ancora in ascolto..." : "Connection refused - Gateway not listening yet...";
    public static string RequestTimeout => IsItalian ? "Timeout richiesta - Il Gateway risponde lentamente..." : "Request timeout - Gateway slow to respond...";
    public static string GatewayCheckTimeout => IsItalian ? "⚠️ Timeout controllo Gateway" : "⚠️ Gateway check timed out";
    public static string ServiceRunningGatewayNoResponse => IsItalian 
        ? "Il servizio è in esecuzione ma il gateway non ha risposto. Ultimo errore: {0}" 
        : "Service is running but gateway didn't respond. Last error: {0}";
    public static string GatewayRunningStatusTimeout => IsItalian ? "Gateway in esecuzione | Timeout stato" : "Gateway running | Status timeout";
}
