# Documentação Técnica do Fork Leo (LeoMarques1206/pos-cg-d297/tree/leo)

Esta documentação fornece uma visão aprofundada da arquitetura, do fluxo de dados e dos componentes de um branch específico de um fork desse projeto, o branch do fork em questão tendo sido elaborado pelo membro da equipe chamado Leonardo. O referido documento serve como base para guiar a integração do progresso desse branch com o progresso do Leonardo.

---

## 1. Visão Geral do Projeto

Este repositório é composto por dois ecossistemas principais que interagem entre si:

1. **Unity Project (Core)**: Um ambiente tridimensional e bidimensional interativo (utilizando Universal Render Pipeline - URP) projetado para visualizar conceitos, métricas e metodologias de papers acadêmicos. Ele utiliza interfaces dinâmicas (cartões tridimensionais, gráficos de barra e tabelas interativas) e agentes virtuais animados por voz que reagem em tempo real a arquivos de legenda SRT.
2. **PaperCave (Backend Agêntico)**: Uma pipeline em Python baseada em **CrewAI** que consome PDFs acadêmicos e, por meio de um fluxo multi-agente, gera perfis estruturados (`PaperProfile`) e planos de design (`DesignPlan`) em formato JSON. *Nota: Esta seção está atualmente desatualizada no repositório, mas as diretrizes de integração permanecem válidas.*

---

## 2. Arquitetura da Parte Unity

A parte Unity é o coração do projeto. O foco é a criação de exibições dinâmicas que evitam configurações fixas (*hard-coded*), permitindo que layouts, textos e imagens se ajustem de acordo com os dados fornecidos.

Abaixo estão os subsistemas principais do Unity:

### A. Instanciação Dinâmica de Cenas (`JsonSceneInstantiator`)
O script [JsonSceneInstantiator.cs](file:///d:/3DCG/Fork%20Leonardo/pos-cg-d297/Assets/Scripts/JsonSceneInstantiator.cs) lê uma string JSON contendo a estrutura de objetos e reconstrói a cena programaticamente em tempo de execução.

* **Fluxo de Funcionamento**:
  1. O JSON é desserializado para a estrutura `SceneData`.
  2. O script instancia o prefab de título e o posiciona na âncora correspondente (`anchorTitle`), injetando o título do paper em um componente `TMP_Text`.
  3. Para cada objeto na lista `gameObjects`, ele seleciona o prefab apropriado com base no `displayType` ("text", "image", "table", "graph"/"chart").
  4. O objeto é instanciado em um dos slots pré-definidos (`anchorSlots`).
  5. **Injeção de Metadados**: O script anexa o componente [GameObjectMeta.cs](file:///d:/3DCG/Fork%20Leonardo/pos-cg-d297/Assets/Scripts/GameObjectMeta.cs) ao objeto instanciado, salvando informações conceituais como metáfora visual, tipo de interação e justificativa científica.
  6. Para tabelas e gráficos, ele delega a montagem aos construtores específicos ([TableBuilder.cs](file:///d:/3DCG/Fork%20Leonardo/pos-cg-d297/Assets/Scripts/TableBuilder.cs) e [BarChartBuilder.cs](file:///d:/3DCG/Fork%20Leonardo/pos-cg-d297/Assets/Scripts/BarChartBuilder.cs)).

---

### B. Tabelas e Gráficos Interativos (World Space UI)
A visualização de dados quantitativos utiliza componentes de Canvas renderizados em World Space.

```
+---------------------------------------+
|  [TableBuilder / BarChartBuilder]    |
|                  |                    |
|          Gera células/barras          |
|                  |                    |
|      Adiciona BoxColliders +          |
|    [TableRowHover / BarChartBarHover] |
+---------------------------------------+
                   |
     Physics.Raycast (Mouse Hover)
                   |
+------------------v--------------------+
|  - Desloca Z (Pop-up tridimensional)  |
|  - Altera cor de destaque             |
|  - Dispara BorderPulse no card alvo   |
|  - Troca o Sprite da imagem de foco   |
+---------------------------------------+
```

* **TableBuilder**:
  * Monta uma grade de RectTransforms dinamicamente com base nas linhas e colunas fornecidas.
  * Se `useStaticPaperData` for verdadeiro, ele substitui os dados dinâmicos por dados estáticos referentes ao mapeamento de blend shapes faciais de personagens (Alina, Asuna, Atticus, etc.).
  * Cada linha recebe um componente [TableRowHover.cs](file:///d:/3DCG/Fork%20Leonardo/pos-cg-d297/Assets/Scripts/TableRowHover.cs) e um `BoxCollider` calculado dinamicamente para cobrir a área da linha.
* **BarChartBuilder**:
  * Constrói eixos, linhas de grade e rótulos de texto de forma procedural.
  * Cria barras verticais e anexa [BarChartBarHover.cs](file:///d:/3DCG/Fork%20Leonardo/pos-cg-d297/Assets/Scripts/BarChartBarHover.cs) a cada uma delas.
* **Mecânica de Hover**:
  * Como os elementos estão em World Space, o sistema não utiliza a detecção tradicional de UI do EventSystem. Em vez disso, os scripts rodam um `Physics.Raycast` a partir do mouse utilizando o `BoxCollider` do elemento.
  * Ao detectar a entrada do mouse (`EnterHover`), a linha ou barra translada ao longo do eixo Z local (indo para a frente da tela) usando interpolação suave (`Vector3.Lerp`).
  * O sistema altera a cor da imagem (`Image.color`) e solicita que a imagem de foco da exposição (ex: `"ExemplosExpressoesGeradas"`) troque o seu Sprite pelo arquivo correspondente em `Assets/Resources/i[index].png`.
  * Adicionalmente, busca um componente `BorderPulse` no card pai e ativa o efeito de pulsação luminosa em suas bordas.

---

### C. Sistema de Cartões Interativos 3D (`Card3D`)
Os cartões em `PaperCave_Cards_3D_Leo.unity` são representações interativas modulares.

* **Card3D.cs**: Controla o estado de abertura do cartão (Collapsed vs. Expanded). Quando aberto, o cartão escala (`expandScale`, padrão: 1.4x) e flutua para a frente no eixo Z (`floatForward`, padrão: 0.5 unidades) em direção à câmera por meio de uma interpolação suave.
* **Card3DController.cs**: Driver global de input da cena (utiliza o **novo Unity Input System**).
  * Realiza Raycasts da câmera.
  * Se clicar em um cartão sem arrastar, alterna seu estado expandido (`Toggle`).
  * Se o cursor for arrastado além de um limiar de pixels (`clickPixelThreshold`), o cartão entra no modo de arrasto físico. Ele segue a projeção do ponteiro do mouse sobre um plano matemático virtual em Z = 0 e é puxado em direção ao jogador (`heldForwardZ`). Quando solto, ele define sua nova posição de repouso (`SetRest`) sem snapping.
* **CardContentFitter.cs**: Script crítico de redimensionamento dinâmico. Ele impede que textos estourem ou que imagens fiquem distorcidas.
  * **Medição de Texto**: Usa `TextMeshProUGUI.GetPreferredValues` para calcular a altura exata necessária para o título, subtítulo e descrição do cartão, ajustando a escala vertical global do RectTransform do cartão.
  * **Layout Adaptativo**: Se a imagem inserida for horizontal (*landscape*), ele expande a largura do cartão (até um limite definido por `maxLandscapeWidth`) para acomodar a imagem e organiza o conteúdo em colunas ou faixas adequadas.
* **ImageAspectFitter.cs** e **ImageNormalizer.cs**: Garantem que as imagens sejam renderizadas com a proporção (*aspect ratio*) correta. Eles usam uma estratégia de enquadramento "contain" (a imagem cabe inteira na área de exibição sem cortar e sem distorcer) aplicando uma escala uniforme, mesmo que o transform pai tenha sofrido redimensionamento não-uniforme.
* **BorderPulse.cs**: Controla quatro GameObjects de borda (`Border_Top/Bottom/Left/Right`). Quando ativado, ele cria instâncias exclusivas de seus materiais, habilita a palavra-chave de emissão (`_EMISSION`) e faz um pulso de cor suave senoidal e brilhante em tempo real no material de renderização.
* **CardFollower.cs**: Como os BoxColliders físicos precisam estar fora de hierarquias de UI altamente escaladas para evitar problemas de colisão, este script permite que tabelas ou imagens em World Space flutuem perfeitamente em cima de um cartão interativo 3D, espelhando sua posição e rotação no `LateUpdate`.

---

### D. Visualizadores de Animação por Quadros (`AnimationFrameView` e `AnimationFrameView3D`)
Usado para ilustrar processos sequenciais divididos em etapas.
* Permitem avançar ou retroceder por uma sequência de estruturas de dados `Frame` (rótulo, descrição e sprite/textura).
* Suportam transições suaves por deslizamento lateral (*slide*) ou esmaecimento (*fade*) utilizando co-rotinas.
* **Card3DButton.cs**: Representa um botão físico 3D na cena que interage com o `Card3DController` e dispara funções no `AnimationFrameView3D`.
* **Auto-Avanço**: Se o usuário não interagir e a opção `looping` estiver ativada, as etapas avançam sozinhas a cada ciclo (`autoAdvanceInterval`).

---

### E. Sistema de Dublagem e Animação de Personagens (`BirdAnimator`)
O projeto conta com avatares de pássaros (Moema, Moemo) que interpretam áudios de podcasts explicativos dos papers científicos acadêmicos.

* **SRTParser.cs**: Um parser customizado de arquivos `.srt` salvos como `TextAsset`. Ele decodifica os timestamps de início e fim, identifica o locutor (ex: `"Sarah:"`, `"John:"`) e extrai o texto do diálogo.
* **BirdAnimator.cs**: Controla o estado de animação do sprite 2D do pássaro.
  * Alterna entre os estados de repouso (`Idle`) e fala (`Talk`).
  * Gerencia a cabeça do pássaro (`headObject`), desativando-a quando animações especiais que redesenham o corpo inteiro estão em execução.
  * Permite transições de cross-fade suaves entre frames de animação utilizando uma camada secundária temporária (`_FadeLayer`).
  * Possui suporte a animações bônus de comportamento aleatório (como piscar de olhos ou gestos corporais) que ocorrem com base em probabilidades (`chance`).
* **BirdAnimatorController.cs**: Sincroniza a reprodução do arquivo de áudio (`AudioSource`) com a animação.
  * Ele lê continuamente o tempo de execução do áudio e verifica as entradas do arquivo de legenda.
  * Se o tempo do áudio estiver dentro da janela de fala do locutor atribuído (aplicando tolerâncias de início/fim como `startOffset` e `endOffset`), o pássaro entra em estado de `Talk()`; caso contrário, volta ao `Idle()`.
  * **Gatilhos por Palavra-Chave**: Ele analisa o texto falado e, se encontrar termos específicos (ex: `"AI"`, `" thyroid"`, `"biomes"`), força imediatamente a execução de uma animação bônus expressiva (`ForceBonusAnimation`).

---

### F. Geração Dinâmica de Exibições de Estudo (`ClaudeAdjustedTestBuilder`)
O script [ClaudeAdjustedTestBuilder.cs](file:///d:/3DCG/Fork%20Leonardo/pos-cg-d297/Assets/ClaudeAdjustedTest/Scripts/ClaudeAdjustedTestBuilder.cs) é um Bootstrap de cena executável que monta programaticamente uma exposição de museu científico em 3D sobre o paper de síntese de expressões faciais. Ele cria e posiciona 5 instalações interativas, espaçadas uniformemente (14 unidades no eixo X):

| Instalação | Componente Unity | Descrição Visual / Mecânica |
| :--- | :--- | :--- |
| **1. Pipeline Terminal** | `PipelineTerminal` | Exibe a arquitetura do sistema RAG. Instancia 5 nós (Unity Plugin, RAG App, etc.) com setas direcionais e telas emissivas retroiluminadas. Exibe tooltips ao interagir. |
| **2. Face Calibration Pod** | `FaceCalibrationPod` | Demonstra o mapeamento de Blend Shapes para Action Units (FACS). Instancia um pedestal futurista com uma cabeça holográfica rotativa que exibe linhas de escaneamento lasers emissivos e dados de AUs flutuantes. |
| **3. Emotion Fidelity Display** | `EmotionFidelityDisplay` | Um gráfico tridimensional interativo apresentando os resultados de fidelidade emocional avaliados na escala Likert-5. Inclui a renderização procedural de barras, eixos e linhas de média estatística. |
| **4. FACS Codex** | `FACSCodex` | Apresenta o vocabulário compartilhado de expressões de Paul Ekman. Instancia fichas interativas (*chips*) de emoções que destacam os músculos faciais ativos (AUs) correspondentes no busto 3D ao serem clicadas. |
| **5. Limitation Shard** | `LimitationShard` | Ilustra as ameaças de validade do estudo. Apresenta um cristal tridimensional levitando, rachado por linhas vermelhas brilhantes, cercado por balões de diálogo interativos que expandem para mostrar detalhes sobre latência, precisão e distorções (LLM drift). |

---

## 3. PaperCave (Pipeline de Agentes Python - CrewAI)

O diretório `PaperCave` contém uma solução baseada em CrewAI projetada para automação de design pedagógico. 

### Fluxo de Trabalho do Backend
1. **Reader (`reader.py`)**: Extrai e limpa o texto completo do PDF inserido.
2. **Summarizer (`summarizer.py`)**: Produz um sumário estruturado dividido em blocos temáticos importantes.
3. **Classifier (`classifier.py`)**: Classifica a contribuição do artigo e seleciona um dos 5 perfis pedagógicos adequados (Explorer, Operator, Comparator, Tuner, Observer), salvando os dados em `03_classifier_output.json` seguindo o Pydantic model `PaperProfile`.
4. **Designer (`designer.py`)**: Utiliza o perfil gerado e realiza pesquisas semânticas dinâmicas (RAG) no paper original para produzir o plano de design detalhado de objetos interativos (`04_designer_output.json` seguindo o Pydantic model `DesignPlan`).

### Mecanismo de Robustez do Backend
* **Thinking Stripper**: Limpa blocos de pensamento de modelos como DeepSeek R1 (`<think>...</think>`) antes de tentar realizar o parsing para JSON.
* **Auto-correção de Schemas**: Se a geração falhar ao validar as regras do Pydantic, o CrewAI executa até 3 tentativas adicionais enviando o relatório de erro do compilador diretamente de volta ao prompt do LLM.

---

## 4. Estrutura e Natureza dos Dados

Qualquer desenvolvimento paralelo deve respeitar os seguintes contratos de dados:

### A. Contrato JSON de Cenários (Unity Ingestion Schema)
Este é o JSON esperado por componentes como o `JsonSceneInstantiator`:

```json
{
  "paperTitle": "Título do Artigo Científico",
  "centralContribution": "Descrição textual da contribuição principal",
  "gameObjects": [
    {
      "suggestedName": "NomeDoObjetoNaHierarquia",
      "conceptualOrigin": "Seção do paper de onde o conceito foi extraído",
      "category": "Categoria conceitual (ex: Algoritmo, Métrica, Interface)",
      "visualMetaphor": "Metáfora visual a ser exibida (texto ou objeto 3D)",
      "behaviourHint": "Dicas de física ou animação (ex: flutuando, rotacionando)",
      "interactionType": "Tipo de interação (ex: Click, Hover, Drag)",
      "whyThisFive": "Justificativa científica baseada nas teorias pedagógicas",
      "relatedImage": "Nome da imagem associada no projeto (se aplicável)",
      "displayType": "text | image | table | graph",
      "rows": 3,
      "columns": 4,
      "tableHeaders": "Cabeçalho 1 | Cabeçalho 2 | Cabeçalho 3",
      "tableRows": "Valor 1 | Valor 2 | Valor 3; Valor A | Valor B | Valor C"
    }
  ]
}
```

### B. Formato de Legendas e Locutores (`SRTParser`)
Para alimentar corretamente o `BirdAnimatorController`, as legendas localizadas em `Assets/Resources/` devem seguir a nomenclatura padronizada de arquivos e o formato de identificação de locutores:

```srt
1
00:00:00,800 --> 00:00:10,500
Sarah: Welcome to our podcast!

2
00:00:10,900 --> 00:00:15,200
John: Today we will explore dynamic facial expressions.
```

---

## 5. Diretrizes para Integração e Comunicação entre Módulos

Atualmente, o projeto Unity e o backend PaperCave operam de forma assíncrona baseada em arquivos estáticos (Unity lê os arquivos gerados manualmente inserindo as strings ou lendo arquivos do diretório `Resources`).

Para desenvolvedores interessados em reativar a comunicação ou integrá-los de forma dinâmica, são propostas três rotas principais:

### Rota A: Integração Local via File-Watcher (Recomendada para Prototipagem)
O Unity pode escutar o diretório de outputs do Python.
1. O script Python salva os JSONs finais na pasta `PaperCave/outputs/{nome_do_pdf}/04_designer_output.json`.
2. No Unity, um script com um monitor de arquivos (`System.IO.FileSystemWatcher`) detecta novos arquivos gravados naquela pasta em tempo de desenvolvimento ou execução local.
3. Ao detectar a mudança, o Unity executa automaticamente o método `InstantiateFromJson()` do `JsonSceneInstantiator`, permitindo ver atualizações de layout na cena quase em tempo real.

### Rota B: Comunicação via Web API Local (REST)
Transformar o PaperCave em um microsserviço leve.
1. Envolver a execução do `crew.py` em uma API simples usando **FastAPI** ou **Flask** rodando localmente (ex: `http://localhost:8000/process-pdf`).
2. O Unity realiza requisições HTTP (`UnityWebRequest`) enviando o caminho do PDF ou o próprio arquivo.
3. O servidor Python executa a pipeline CrewAI e retorna o JSON estruturado diretamente no corpo da resposta HTTP.
4. O Unity recebe o JSON no callback e atualiza o estado da cena dinamicamente.

### Rota C: Controle Dinâmico via WebSocket (Para Geração em Tempo Real)
Para visualizações onde o progresso de cada agente deve ser exibido na tela no Unity:
1. O backend transmite mensagens WebSocket a cada etapa concluída pelo CrewAI (Reader -> Summarizer -> Classifier -> Designer).
2. O Unity recebe as atualizações parciais de progresso e anima componentes da interface (ex: preenchendo barras de carregamento ou ativando os nós no "Pipeline Terminal" dinamicamente conforme os agentes do backend trabalham).

---

## 6. Configurações e Requisitos de Setup

### Requisitos da Categoria Unity
* **Unity Version**: Recomendada versão LTS 2022.3 ou superior.
* **Render Pipeline**: Universal Render Pipeline (URP). Os materiais emissivos e os shaders transparentes foram configurados para o pipeline URP.
* **Input Handling**: Configurado para usar o **Input System Package** (Active Input Handling nas configurações do projeto deve conter `Input System Package (New)` ou `Both`).
* **Text Rendering**: Utiliza **TextMesh Pro**. Certifique-se de importar os recursos essenciais do TMP (`Window > TextMeshPro > Import TMP Essential Resources`) caso ocorram erros de fonte.

### Requisitos da Categoria PaperCave (Backend)
* **Python**: Versão 3.11+.
* **Instalação de Bibliotecas**: Executar `pip install -r requirements.txt` no ambiente virtual `.venv`.
* **Credenciais**: Criar o arquivo `.env` na raiz da pasta `PaperCave` definindo chaves de provedor (ex: `GEMINI_API_KEY`, `OPENAI_API_KEY` ou configurar chaves locais em `config/config.yaml`).
