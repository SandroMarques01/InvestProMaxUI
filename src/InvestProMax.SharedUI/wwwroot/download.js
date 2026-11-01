// Referenciado pelos heads (Fase 7) em seu index.html/wwwroot junto com app.css — ver
// comentário em Pages/Configuracoes.razor sobre por que isto precisa de JS interop mesmo
// dentro de um MAUI Blazor Hybrid (o WebView também expõe o DOM do navegador).
window.investProMaxDownloadFile = (nomeArquivo, base64) => {
    const link = document.createElement("a");
    link.href = `data:application/octet-stream;base64,${base64}`;
    link.download = nomeArquivo;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.investProMaxGetTheme = () => {
    try {
        return localStorage.getItem("investpromax.theme") || "light";
    } catch {
        return "light";
    }
};

window.investProMaxSetTheme = (theme) => {
    try {
        localStorage.setItem("investpromax.theme", theme);
    } catch { }
    if (theme === "dark") {
        document.documentElement.setAttribute("data-theme", "dark");
    } else {
        document.documentElement.removeAttribute("data-theme");
    }
};

(() => {
    const savedTheme = window.investProMaxGetTheme();
    if (savedTheme === "dark") {
        document.documentElement.setAttribute("data-theme", "dark");
    }
})();
