import express from 'express';
import makeWASocket, {
    useMultiFileAuthState,
    DisconnectReason,
    fetchLatestBaileysVersion
} from '@whiskeysockets/baileys';
import pino from 'pino';
import QRCode from 'qrcode';
import qrcodeTerminal from 'qrcode-terminal';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const PORT = process.env.PORT || 8080;
const API_KEY = process.env.AUTHENTICATION_API_KEY || 'BarberShopEvolutionKey2026';

const app = express();
app.use(express.json());

let sock = null;
let currentQR = null;
let currentQRDataUrl = null;
let isConnected = false;
let connectedUser = null;

async function startWhatsApp() {
    const { state, saveCreds } = await useMultiFileAuthState(path.join(__dirname, 'auth_info'));
    const { version } = await fetchLatestBaileysVersion();

    sock = makeWASocket({
        version,
        logger: pino({ level: 'silent' }),
        auth: state,
        printQRInTerminal: false,
        browser: ['BarberShop Local', 'Chrome', '1.0.0']
    });

    sock.ev.on('creds.update', saveCreds);

    sock.ev.on('connection.update', async (update) => {
        const { connection, lastDisconnect, qr } = update;

        if (qr) {
            currentQR = qr;
            currentQRDataUrl = await QRCode.toDataURL(qr, { width: 300, margin: 2 });
            console.log('\n======================================================');
            console.log('📲 NOVO QR CODE GERADO! ESCANEIE NO SEU WHATSAPP:');
            console.log('Acesse também pelo navegador em: http://localhost:' + PORT);
            console.log('======================================================\n');
            qrcodeTerminal.generate(qr, { small: true });
        }

        if (connection === 'open') {
            isConnected = true;
            currentQR = null;
            currentQRDataUrl = null;
            connectedUser = sock.user?.id ? sock.user.id.split(':')[0] : 'Conectado';
            console.log('\n✅ [WHATSAPP CONECTADO COM SUCESSO!]');
            console.log(`Número Conectado: ${connectedUser}`);
            console.log(`Pronto para enviar notificações do BarberShop!\n`);
        }

        if (connection === 'close') {
            isConnected = false;
            const statusCode = lastDisconnect?.error?.output?.statusCode;
            const shouldReconnect = statusCode !== DisconnectReason.loggedOut;
            console.log(`⚠️ Conexão fechada (${statusCode}). Reconectando: ${shouldReconnect}`);
            if (shouldReconnect) {
                setTimeout(startWhatsApp, 3000);
            } else {
                console.log('❌ Sessão desconectada. Novo QR Code será gerado.');
                startWhatsApp();
            }
        }
    });
}

// Middleware de autenticação opcional via API Key
function checkApiKey(req, res, next) {
    const apikey = req.headers['apikey'] || req.query.apikey;
    if (API_KEY && apikey && apikey !== API_KEY) {
        return res.status(401).json({ error: 'Chave de API inválida.' });
    }
    next();
}

// Rota de status em JSON
app.get('/status', (req, res) => {
    res.json({
        connected: isConnected,
        user: connectedUser,
        hasQr: !!currentQRDataUrl,
        qr: currentQRDataUrl
    });
});

// Endpoint compatível com Evolution API: POST /message/sendText/:instance
app.post('/message/sendText/:instance', checkApiKey, async (req, res) => {
    try {
        const { number, text, textMessage } = req.body;
        const rawMessage = text || textMessage?.text || '';

        if (!rawMessage) {
            return res.status(400).json({ error: 'O campo text é obrigatório.' });
        }

        if (!number) {
            return res.status(400).json({ error: 'O campo number é obrigatório.' });
        }

        if (!isConnected || !sock) {
            return res.status(503).json({
                error: 'WhatsApp desconectado. Por favor, escaneie o QR Code em http://localhost:' + PORT
            });
        }

        // Formata o número (garante DDI e formato WhatsApp @s.whatsapp.net)
        let cleanNumber = String(number).replace(/\D/g, '');
        if (!cleanNumber.startsWith('55') && (cleanNumber.length === 10 || cleanNumber.length === 11)) {
            cleanNumber = '55' + cleanNumber;
        }

        const jid = `${cleanNumber}@s.whatsapp.net`;

        const sent = await sock.sendMessage(jid, { text: rawMessage });

        return res.status(200).json({
            key: {
                remoteJid: jid,
                fromMe: true,
                id: sent.key.id
            },
            message: {
                conversation: rawMessage
            },
            status: 'ACCEPTED'
        });
    } catch (err) {
        console.error('Erro ao enviar mensagem:', err);
        return res.status(500).json({ error: err.message });
    }
});

// Página inicial moderna para escanear o QR Code
app.get('/', (req, res) => {
    res.send(`<!DOCTYPE html>
<html lang="pt-BR">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>BarberShop - WhatsApp Evolution Gateway</title>
    <link href="https://fonts.googleapis.com/css2?family=Cinzel:wght@600;700&family=Inter:wght@300;400;500;600&display=swap" rel="stylesheet">
    <style>
        :root {
            --bg-color: #111111;
            --card-bg: #1A1A1A;
            --gold: #C8A96B;
            --gold-light: #FFE8A3;
            --text-light: #F5F3EE;
            --text-muted: #A6A6A6;
            --border-color: rgba(200, 169, 107, 0.25);
            --success: #25D366;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: 'Inter', sans-serif;
            background-color: var(--bg-color);
            color: var(--text-light);
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 1.5rem;
        }
        .container {
            background: var(--card-bg);
            border: 1px solid var(--border-color);
            border-radius: 16px;
            max-width: 520px;
            width: 100%;
            padding: 2.5rem;
            text-align: center;
            box-shadow: 0 10px 40px rgba(0,0,0,0.6);
        }
        .brand {
            font-family: 'Cinzel', serif;
            font-size: 1.6rem;
            color: var(--gold);
            letter-spacing: 2px;
            margin-bottom: 0.5rem;
        }
        .subtitle {
            font-size: 0.9rem;
            color: var(--text-muted);
            margin-bottom: 2rem;
        }
        .qr-card {
            background: #ffffff;
            border-radius: 12px;
            padding: 1.25rem;
            display: inline-block;
            margin: 1rem 0 1.5rem 0;
            box-shadow: 0 4px 20px rgba(200, 169, 107, 0.2);
            min-width: 250px;
            min-height: 250px;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        .qr-card img {
            width: 260px;
            height: 260px;
            display: block;
        }
        .status-badge {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 0.5rem 1.25rem;
            border-radius: 50px;
            font-size: 0.85rem;
            font-weight: 500;
            margin-bottom: 1.5rem;
        }
        .status-waiting {
            background: rgba(255, 193, 7, 0.15);
            color: #FFC107;
            border: 1px solid rgba(255, 193, 7, 0.3);
        }
        .status-connected {
            background: rgba(37, 211, 102, 0.15);
            color: var(--success);
            border: 1px solid rgba(37, 211, 102, 0.3);
        }
        .instructions {
            text-align: left;
            background: rgba(255,255,255,0.03);
            border: 1px solid rgba(255,255,255,0.06);
            border-radius: 10px;
            padding: 1rem 1.25rem;
            font-size: 0.85rem;
            line-height: 1.6;
            color: var(--text-muted);
        }
        .instructions ol {
            padding-left: 1.2rem;
        }
        .instructions strong {
            color: var(--text-light);
        }
        .connected-box {
            padding: 2rem 1rem;
        }
        .connected-box h3 {
            color: var(--success);
            margin-bottom: 0.5rem;
        }
        .connected-box p {
            color: var(--text-muted);
            font-size: 0.95rem;
        }
        .btn-test {
            margin-top: 1.5rem;
            background: linear-gradient(135deg, var(--gold), #9E7D3B);
            color: #111;
            font-weight: 600;
            padding: 0.75rem 1.75rem;
            border: none;
            border-radius: 8px;
            cursor: pointer;
            text-decoration: none;
            display: inline-block;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="brand">BARBERSHOP</div>
        <div class="subtitle">Gateway Local de WhatsApp (Compatível Evolution API)</div>

        <div id="statusContainer">
            <div class="status-badge status-waiting" id="statusBadge">
                <span>●</span> <span id="statusText">Verificando conexão...</span>
            </div>
        </div>

        <div id="qrArea">
            <div class="qr-card">
                <img id="qrImg" src="${currentQRDataUrl || ''}" alt="QR Code" style="${currentQRDataUrl ? '' : 'display:none;'}">
                <div id="qrLoading" style="${currentQRDataUrl ? 'display:none;' : ''} color:#111; font-size:0.9rem;">
                    Gerando QR Code...
                </div>
            </div>

            <div class="instructions">
                <strong>Como conectar:</strong>
                <ol>
                    <li>Abra o <strong>WhatsApp</strong> no seu celular.</li>
                    <li>Toque em <strong>Mais opções</strong> (⋮) ou <strong>Configurações</strong>.</li>
                    <li>Toque em <strong>Aparelhos conectados</strong> > <strong>Conectar aparelho</strong>.</li>
                    <li>Aponte a câmera para o QR Code acima.</li>
                </ol>
            </div>
        </div>

        <div id="connectedArea" style="display:none;" class="connected-box">
            <h3>🎉 WhatsApp Conectado!</h3>
            <p>Seu número está pronto para enviar notificações de agendamentos da BarberShop automaticamente e sem custos.</p>
            <p style="margin-top: 0.75rem;"><strong style="color:var(--gold);">Número ativo:</strong> <span id="connectedPhone">-</span></p>
            <a href="http://localhost:5296/Admin" class="btn-test">Ir para o Painel BarberShop</a>
        </div>
    </div>

    <script>
        async function checkStatus() {
            try {
                const res = await fetch('/status');
                const data = await res.json();

                const badge = document.getElementById('statusBadge');
                const text = document.getElementById('statusText');
                const qrArea = document.getElementById('qrArea');
                const connectedArea = document.getElementById('connectedArea');
                const qrImg = document.getElementById('qrImg');
                const qrLoading = document.getElementById('qrLoading');
                const phoneSpan = document.getElementById('connectedPhone');

                if (data.connected) {
                    badge.className = 'status-badge status-connected';
                    text.innerText = 'WhatsApp Conectado';
                    qrArea.style.display = 'none';
                    connectedArea.style.display = 'block';
                    phoneSpan.innerText = '+' + data.user;
                } else {
                    badge.className = 'status-badge status-waiting';
                    text.innerText = 'Aguardando Leitura do QR Code';
                    qrArea.style.display = 'block';
                    connectedArea.style.display = 'none';

                    if (data.qr) {
                        qrImg.src = data.qr;
                        qrImg.style.display = 'block';
                        qrLoading.style.display = 'none';
                    } else {
                        qrImg.style.display = 'none';
                        qrLoading.style.display = 'block';
                    }
                }
            } catch (e) {
                console.error(e);
            }
        }

        setInterval(checkStatus, 2500);
        checkStatus();
    </script>
</body>
</html>`);
});

app.listen(PORT, () => {
    console.log(`\n======================================================`);
    console.log(`🚀 [GATEWAY WHATSAPP] Servidor rodando em http://localhost:${PORT}`);
    console.log(`🔑 Chave de API: ${API_KEY}`);
    console.log(`======================================================\n`);
    startWhatsApp();
});
