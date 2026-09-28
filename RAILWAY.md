# Hospedagem da API e do MySQL no Railway

## Estado atual

O Dockerfile prepara a API .NET 10 para receber a porta definida pelo Railway.
Os arquivos appsettings com a conexão local são excluídos do Git e da imagem Docker.
No servidor, a conexão é configurada pela variável ConnectionStrings__BancoJogo.

Os arquivos foram preparados, mas a publicação e a execução no Railway ainda não foram verificadas.

A API atual usa um save compartilhado: jogador_id = 1. O botão Novo Jogo limpa todas as linhas de inventario_fase e itens_coletados. Antes de distribuir para jogadores com saves individuais, implemente identificação e autenticação do jogador e restrinja consultas, gravações e exclusões aos dados desse jogador.

## 1. Enviar a API para um repositório privado

Crie um repositório privado no GitHub para esta pasta:

    C:\Users\Bruno\source\repos\Unreal_PI

O Dockerfile precisa estar na raiz desse repositório, ao lado da pasta Unreal_PI que contém o arquivo .csproj.
Revise os arquivos selecionados antes do commit. Não envie senhas, arquivos .env ou backups com dados pessoais.

## 2. Criar o banco no Railway

Crie um projeto no Railway e adicione um serviço MySQL. Use o nome MySQL para que as referências abaixo funcionem.
Mantenha a comunicação API/banco na rede privada do projeto.

Crie as três tabelas no banco novo usando deploy/schema.mysql.sql. Esse arquivo contém somente a estrutura das tabelas, sem os saves locais.
Para importar pelo MySQL Workbench, use os dados de conexão pública do serviço. Se necessário, habilite acesso público temporariamente para a importação e remova depois. Selecione o banco informado em MYSQLDATABASE antes de executar o arquivo.

## 3. Adicionar o serviço da API

No mesmo projeto do Railway, adicione um serviço a partir do repositório privado do GitHub.
Use a raiz do repositório como diretório de implantação. O Railway deve detectar o Dockerfile.

No serviço da API, configure esta variável:

Nome:

    ConnectionStrings__BancoJogo

Valor, referenciando o serviço MySQL:

    Server=${{MySQL.MYSQLHOST}};Port=${{MySQL.MYSQLPORT}};Database=${{MySQL.MYSQLDATABASE}};User ID=${{MySQL.MYSQLUSER}};Password=${{MySQL.MYSQLPASSWORD}};

Se o serviço do banco tiver outro nome, substitua MySQL nas referências.
Não use localhost e não coloque a senha no código da Unreal.

## 4. Gerar o endereço da API

Depois da implantação, abra as configurações de rede do serviço da API e gere um domínio público HTTPS.
A API escuta a porta PORT fornecida pelo Railway, com 8080 como alternativa.
Confira os logs de implantação e de execução antes de usar no jogo.

## 5. Conectar o jogo

Com o endereço real do serviço, substitua as URLs http://localhost:5070 no ProgressHttpActor.cpp pelo endereço HTTPS da API.
Recompile com a Unreal fechada e gere uma nova versão do jogo.
O jogador usa a API online; ele não precisa instalar MySQL nem executar a API no próprio computador.

## Custos e documentação

Confira o preço e os limites do plano na sua conta antes de publicar. O teste gratuito tem limites; o consumo de API e banco pode gerar cobrança.

- ASP.NET Core: https://docs.railway.com/guides/aspnet-core
- MySQL: https://docs.railway.com/databases/mysql
- Preços: https://railway.com/pricing
