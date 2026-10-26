# Meu Assistente (.NET 8)

Aplicativo desktop para Windows, feito em WPF, com armazenamento SQLite local, cadastro e login, tarefas, agenda, anotações com post-its, assistente local e chat opcional com provedor compatível com a API Chat Completions.

## Recursos

- Cadastro, login e sessão opcional. Senhas novas usam PBKDF2-HMAC-SHA256 com salt aleatório e 600 mil iterações. A senha em texto puro do banco Python é removida após o primeiro login válido.
- CRUD de tarefas, compromissos e anotações. Os filtros de agenda aceitam datas `DD/MM/AAAA`; os registros são vinculados ao ID do usuário.
- Post-its temporários sempre visíveis, acionados a partir de anotações salvas.
- Assistente local em português para consultar e propor mudanças. Ações de criação, conclusão e exclusão aguardam confirmação na interface; títulos ambíguos pedem seleção.
- Chat opcional com streaming, interrupção, ferramentas para propor ações locais, voz do Windows e pesquisa web opcional via OpenRouter.
- Preferências de modelo, tema e cor de destaque. A chave da API é protegida com DPAPI para o usuário Windows atual.
- Ícone na bandeja e notificações de compromissos nos próximos 15 minutos enquanto o aplicativo estiver em execução.

## Dados e migração

O banco ativo fica em `%LOCALAPPDATA%\MeuAssistente\assistente.db`. Na primeira abertura, o app copia `assistente.db` do diretório atual ou do executável; se não localizar, permite selecionar o banco legado. O arquivo Python original não é alterado. Antes de migrar, o app cria ao lado do banco uma cópia de segurança protegida por DPAPI. A cópia só pode ser restaurada pelo mesmo usuário Windows. Migrações usam `PRAGMA user_version`, transação SQLite, preservação de contagens e `integrity_check`.

Os dados locais não são enviados automaticamente ao chat. As mensagens do chat são enviadas ao endpoint configurado; uma ação solicitada pela IA exige confirmação. Reconhecimento e síntese de voz usam recursos locais do Windows. A senha da API não aparece após salvar.

SQLite usa `Microsoft.Data.Sqlite`: o esquema e os repositórios são pequenos, e uma ORM adicionaria complexidade sem benefício prático. O instalador usa Inno Setup 6 para gerar um pacote independente com atalhos e desinstalador; o banco permanece no perfil do usuário.

## Compilar e executar

Requisitos: Windows, SDK .NET 8, acesso aos feeds NuGet durante o restore.

```powershell
dotnet restore .\MeuAssistente.sln
dotnet build .\MeuAssistente.sln -c Release
dotnet test .\MeuAssistente.sln -c Release
dotnet run --project .\src\MeuAssistente.App\MeuAssistente.App.csproj
```

Validação executada: `dotnet build .\MeuAssistente.sln -c Release` passou sem avisos. `dotnet test .\MeuAssistente.sln -c Release` passou com 23 testes, incluindo fixture do esquema Python, conversão de datas, backup protegido por DPAPI e integridade. Uma cópia temporária do banco Python real foi migrada para a versão 2, com contagens preservadas e `integrity_check` aprovado; o original permaneceu intacto. A publicação `win-x64` e o instalador Inno Setup foram testados, incluindo desinstalação e remoção dos atalhos.

## Publicar e gerar instalador

No Windows com SDK .NET 8 e Inno Setup 6 instalados:

```powershell
dotnet publish .\src\MeuAssistente.App\MeuAssistente.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64
iscc .\installer\MeuAssistente.iss
```

O instalador Inno Setup empacota a publicação self-contained. O banco fica no perfil do usuário e não é substituído ao atualizar o programa.
