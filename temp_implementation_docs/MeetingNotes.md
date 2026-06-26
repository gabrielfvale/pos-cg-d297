Anotações Soltas:

 - tratamento de imagens:
 -- Imagens compostas(imagens que são várias imagens juntas, como várias fotos de rostos lado a lado que são efetivamente uma imagem individual, preciso encontrar uma forma de detectar e separar elas em imagens individuais de forma automatizada, a professora se referiu a isso como "Imagens Germinadas")


- 3 classes de carta que podem ser stacks: imagem, gráfico e tabelas
-- todas as imagens, todos os gráficos, e todas as tabelas, em cada stack, na ordem de ocorrência

categorias:

graphical representation
resumo/abstract
contribuição
imagem
gráfico
tabela

---

O que extrair do paper:

Informação para cada classe de carta:

Cartas Individuais(cada uma vai estar carregando informação apropriada dentro de um limite de caracteres)
- Graphical Representation(O elemento visual MAIS IMPORTANTE de todo o paper, esse elemento visual é excluído de qualquer stack abaixo)
- resumo/abstract
- contribuição

Stacks de Cartas(cada stack vai ter N stacked cards, onde N é igual ao número de elementos encontrados no paper em si, sem texto nem nenhum processamento específico para imagens, tabelas e gráficos devem ser transformados em dados e então renderizados apropriadamente na imagem)
- imagem
- gráfico
- tabela



A pipeline deve ser ajustada para se tornar uma extração do que precisamos para cada carta, e o output deve ser um json com todas as informações que o Leo precisa pra montar a carta

Mensagens do Leo:

"""
Mas no geral pensa assim: Eu vou precisar de toda informação necessaria que tem em um paper, entao toda imagem, texto, dados de tabela vai ajudar


O que eu vou fazer é ajustar o estilo dos cards para vertical ou horizontal, e fazer com que a tabela se comunique com a imagem caso a imagem tenha mais de uma (como aquela das varias faces)
"""

---

To-Do
- investigar imagens germinadas
- investigar a pipeline para transformar ela em algo mais focado e alinhado com as intenções
- investigar o "protocolo de comunicação" entre o meu output json(não o unity export, aquilo é desnecessário agora) e o que o Leo recebe
- identificar quaisquer perguntas a fazer pra ele e quaisquer informações a passar pra ele, e documentar elas de forma a mandar na forma de mensagem no whatsapp