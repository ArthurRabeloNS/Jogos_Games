# Steve Cube — Minecraft no Overcooked! 2

Mod para **Overcooked! 2** (BepInEx) que transforma o chef do **Jogador 1** no **Steve do Minecraft** e adiciona
uma mini-experiência estilo Minecraft dentro da cozinha: hotbar, blocos, TNT, Ender Pearl, foguetes, zoom de câmera e HUD.

> Projeto de fã, sem fins lucrativos e sem relação com a Mojang, a Microsoft, a Team17 ou a Ghost Town Games.
> Minecraft e Overcooked! são marcas de seus respectivos donos.

---

## O que o mod faz

- **Steve no lugar do chef (Jogador 1)** — modelo em cubos montado por código, com a skin 64x64 do Steve e animações no estilo Minecraft (caminhada, respiração, balanço do braço ao cortar/lavar).
- **Ferramentas na mão** — quando o chef corta, o Steve segura uma espada, machado ou picareta.
- **Itens flutuando na mão** — o ingrediente que você carrega gira e flutua perto da mão do Steve.
- **Hotbar de 9 slots** com HUD de coração, armadura, fome e barra de XP (só visual).
- **Blocos** — coloque madeira e obsidiana, quebre blocos e até bancadas do mapa.
- **TNT** — pavio de 4 segundos, pisca e explode.
- **Ender Pearl** — arremesse e teleporte.
- **Foguete** — decola e estoura em fogos coloridos.
- **Câmera** — zoom e opção de seguir o Steve.

O Jogador 2 continua com o chef original.

---

## Requisitos

- **Overcooked! 2** (Steam, PC)
- **[BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases)** (Unity Mono) instalado no jogo

---

## Instalação (para jogar)

1. **Instale o BepInEx 5** no Overcooked! 2:
   - Baixe o BepInEx 5 (versão que combina com o seu jogo, x86 ou x64) na [página de releases](https://github.com/BepInEx/BepInEx/releases).
   - Extraia **tudo dentro da pasta do jogo**, ao lado do `Overcooked2.exe`.
   - Exemplo: `C:\Program Files (x86)\Steam\steamapps\common\Overcooked! 2\`
2. **Abra o jogo uma vez e feche.** Isso faz o BepInEx criar as pastas `BepInEx\plugins`, `BepInEx\config` etc.
3. **Copie o `SteveCube.dll`** (da aba *Releases* deste repositório) para:
   ```
   Overcooked! 2\BepInEx\plugins\
   ```
4. Abra o jogo e entre em uma fase. O Steve aparece no lugar do Jogador 1.

> **Skin personalizada (opcional):** a skin do Steve já vem embutida na DLL. Para usar outra, coloque um PNG **64x64** (formato de skin do Minecraft, também funciona 64x32) chamado `steve.png` em `BepInEx\plugins\`. O arquivo tem prioridade sobre a skin embutida.

---

## Controles

| Tecla | Ação |
|---|---|
| `1` – `9` ou **roda do mouse** | Escolhe o slot da hotbar |
| `B` | Coloca o item do slot selecionado na frente do Steve (slots 1, 2, 5 e 6) |
| `U` | Quebra o bloco ou a bancada na frente do Steve |
| `N` | Restaura todas as bancadas quebradas |
| `T` | Arremessa a Ender Pearl (com o slot 3 selecionado) |
| `=` (segurar) / `+` do numpad | Aproximar a câmera |
| `-` (segurar) / `-` do numpad | Afastar a câmera |
| `0` | Reseta o zoom |
| `V` | Liga/desliga a câmera seguindo o Steve |

### Hotbar

| Slot | Item | Como usar |
|---|---|---|
| 1 | Madeira | `B` coloca um bloco sólido de madeira |
| 2 | TNT | `B` coloca; explode após 4 s |
| 3 | Ender Pearl | `T` arremessa e teleporta o Steve |
| 4 | Picareta | Item visual |
| 5 | Obsidiana | `B` coloca; **a TNT não quebra**, só o `U` |
| 6 | Foguete | `B` coloca; decola e vira fogos de artifício |
| 7 – 9 | Itens do jogo | Preenchidos automaticamente com os ingredientes que você pegar; o slot é liberado quando você solta/entrega o item |

Teclas e valores podem ser trocados no arquivo de configuração (veja abaixo).

---

## Configuração

Depois de rodar o jogo uma vez com o mod instalado, os arquivos de config são criados em `BepInEx\config\`:

| Arquivo | Conteúdo |
|---|---|
| `com.seunome.oc2.stevecube.cfg` | Tamanho/posição do Steve, animação, item na mão, debug |
| `com.seunome.oc2.steveblocks.cfg` | Teclas B/U/N, tamanho dos blocos, alcance, o que pode ser quebrado |
| `com.seunome.oc2.steveround2.cfg` | Câmera (zoom, seguir, teclas) e HUD |

Opções principais:

**Steve** (`stevecube.cfg`)
- `AlturaMundo` — altura do Steve no jogo (padrão `1.5`)
- `LarguraBlocos` — `1` é a proporção exata do Minecraft; aumente para engrossar
- `AjusteY` — sobe/desce o Steve se os pés afundarem ou flutuarem
- `RotacaoY` — coloque `180` se o Steve aparecer de costas
- `VelocidadeDaCaminhada`, `FerramentasNaMao`, `EscalaNaMao`, `GiroGrausPorSeg`
- `OcultarPorLayer` — plano B: se o chef original **ainda aparecer** junto com o Steve, ligue esta opção

**Blocos** (`steveblocks.cfg`)
- `ColocarBloco`, `Quebrar`, `RestaurarBancadas` — teclas
- `TamanhoDoBloco`, `AlcanceParaQuebrar`, `LimiteDeBlocos` (o mais antigo some ao passar do limite; padrão 64)
- `QuebrarBancadas` (padrão ligado), `QuebrarEstacoes` (padrão desligado, **pode travar a fase**), `ProtegerBordas`, `MargemDaBorda`

**Câmera e HUD** (`steveround2.cfg`)
- `Zoom` (0 = câmera original, 1 = bem perto), `SeguirSteve`, teclas de zoom
- HUD: `Escala` (0 = automática), `MostrarVidaFomeXP`, `MostrarNumeros`

---

## Como funciona (resumo técnico)

O projeto é composto por três plugins BepInEx dentro da mesma DLL:

| Arquivo | Plugin | Função |
|---|---|---|
| `Plugin.cs` | **Steve Cube** | Monta o Steve e o anima |
| `Blocks.cs` | **Steve Blocks** | Colocar/quebrar blocos |
| `Round2.cs` | **Steve Cube – Round 2** | Hotbar, HUD, câmera, TNT, Ender Pearl, foguete |
| `Skin.cs` | — | Skin do Steve em Base64, embutida na DLL |
| `HudArt.cs` | — | Atlas de pixel art do HUD em Base64 (corações, fome, ícones dos slots) |

**Steve (`Plugin.cs`)**
1. A cada 0,5 s o plugin procura o objeto `Chefs` na cena e acha o `Player 1`.
2. Cria um esqueleto de 6 caixas (cabeça, corpo, 2 braços, 2 pernas) cujas UVs seguem o layout de skin do Minecraft, e posiciona sobre o chef.
3. **Esconde o chef original** de várias formas ao mesmo tempo: `forceRenderingOff`, `enabled = false` a cada quadro e antes de cada câmera (`Camera.onPreCull` / `onPostRender`), e opcionalmente movendo o corpo para uma layer que nenhuma câmera desenha.
4. Lê o estado do chef original (andando, cortando, lavando, segurando item) e converte em animação do Steve com as fórmulas de caminhada/balanço do Minecraft.
5. O ponto de attach do jogo (onde o item carregado aparece) é movido para a mão do Steve.

**Blocos (`Blocks.cs`)**
- Os blocos ficam alinhados a uma grade e têm colisão. O `U` só quebra blocos do Steve e objetos dentro de `Work Surfaces` (e `Gameplay Stations`, se habilitado). Paredes, piso e bordas do mapa nunca quebram.

**Round 2 (`Round2.cs`)**
- A hotbar só controla a **visibilidade** do item segurado. Os ingredientes continuam sob controle do Overcooked.
- A câmera do jogo continua mandando: o mod só desloca a posição dela logo antes de desenhar e devolve logo depois, então os scripts do jogo não percebem.

**Sem arquivos extras:** skin, texturas da TNT/obsidiana e arte do HUD são geradas por código ou embutidas em Base64 na DLL.

---

## Compilando do código-fonte

Requisitos: **.NET SDK** (qualquer versão recente) e o jogo instalado.

1. Clone o repositório.
2. Abra o `SteveCube.csproj` e **altere o caminho do jogo** na linha:
   ```xml
   <OC2>D:\SteamLibrary\steamapps\common\Overcooked! 2</OC2>
   ```
   para a pasta onde o seu Overcooked! 2 está instalado.
3. Compile:
   ```
   dotnet build -c Release
   ```
4. O build **copia automaticamente** o `SteveCube.dll` para `BepInEx\plugins` do jogo.

O projeto referencia as DLLs do Unity do próprio jogo (`Overcooked2_Data\Managed`) e o `BepInEx.dll` (`BepInEx\core`), por isso o BepInEx precisa estar instalado antes de compilar. Alvo: `net35`, C# 7.3.

---

## Solução de problemas

O log fica em `BepInEx\LogOutput.log`.

| Problema | O que fazer |
|---|---|
| Nada acontece | Confira se o BepInEx está instalado (a pasta `BepInEx` existe e o log é criado ao abrir o jogo) e se o `SteveCube.dll` está em `BepInEx\plugins`. |
| O chef original aparece junto com o Steve | Em `stevecube.cfg`, ligue `OcultarPorLayer`. |
| Steve de costas | `RotacaoY = 180` |
| Pés afundando ou flutuando | Ajuste `AjusteY` |
| Steve muito fino/grosso ou alto/baixo | Ajuste `LarguraBlocos` e `AlturaMundo` |
| Steve aparece rosa/preto/sem textura | Veja no log qual shader foi escolhido; o mod procura um shader opaco compatível automaticamente. |
| Fase travou depois de quebrar algo | Desligue `QuebrarEstacoes` e use `N` para restaurar as bancadas. |
| Quero investigar a estrutura do chef | Em `stevecube.cfg`, ligue `GerarDump`: o mod grava `BepInEx\chef_dump2.txt` com a hierarquia do chef. |

---

## Aviso

Mod feito para uso offline/local, para diversão. Pode quebrar com atualizações do jogo. Use por sua conta e risco.

## Créditos

- Feito por **ARTHUR RABELO NUNES**
- [BepInEx](https://github.com/BepInEx/BepInEx)

## Licença

Este projeto está licenciado sob a Licença MIT - veja o arquivo [LICENSE](LICENSE) para mais detalhes.
