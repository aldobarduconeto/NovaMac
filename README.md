# NovaMac for Windows

Gerenciador de máquinas virtuais macOS moderno e de alta performance para Windows 10 e 11. O NovaMac combina uma interface limpa desenvolvida em **WinUI 3 (Windows App SDK)** com a robustez do **QEMU**, aceleração por hardware via **Plataforma do Hipervisor do Windows (WHPX)**, bootloader **OpenCore** e firmware **OVMF UEFI (4MB)**.

---

## Destaques e Funcionalidades

- **Layout Clean & Moderno**: Interface inspirada no design Fluent e macOS, com suporte a materiais translúcidos (**Acrílico**, **Mica** e **Mica Alt**), alternância dinâmica entre tema claro e escuro e cartões visuais organizados.
- **Gerenciamento Multi-VM**: Crie, edite, clone e execute múltiplas instâncias de macOS de forma independente, com alocação automática de portas SSH e VNC/Screen Sharing exclusivas por VM.
- **Catálogo de Versões Abrangente**: Suporte do macOS Big Sur (11), Monterey (12), Ventura (13), Sonoma (14), Sequoia (15), Tahoe (26) até builds experimentais do macOS 27.
- **Integração com Servidores Apple**: Download automatizado de imagens de recuperação (`BaseSystem.dmg`) e pacotes oficiais completos (`InstallAssistant.pkg`) diretamente dos servidores de catálogo da Apple (SUCatalog).
- **Bootloader OpenCore & Firmware UEFI Pré-configurados**: Provisionamento automático de OVMF de 4MB compatível com NVRAM persistente e `OpenCore-config.plist` otimizado para QEMU.
- **Gerador de Identidade Apple (SMBIOS)**: Criação automática de números de série válidos, Board Serial (MLB de 17 caracteres), System UUID e endereço MAC ROM para cada máquina virtual.
- **Aceleração Nativa por Hardware**: Execução com latência reduzida utilizando aceleração WHPX do Windows (`-accel whpx,kernel-irqchip=off`).
- **Dashboard de Diagnóstico**: Painel integrado para validação instantânea dos componentes de virtualização, suporte a CPU e status do hipervisor, com atalhos de instalação do QEMU via Winget e ativação do WHPX.

---

## Requisitos de Sistema

| Requisito | Detalhes |
| :--- | :--- |
| **Sistema Operacional** | Windows 10 (versão 1809 ou superior) ou Windows 11 (64-bit) |
| **Processador (CPU)** | Intel Core/Xeon (mínimo Haswell) ou AMD Ryzen com suporte a virtualização (VT-x / AMD-V) e instruções SSE4.2 / AVX2 |
| **Hipervisor** | Recurso **Plataforma do Hipervisor do Windows** (*Windows Hypervisor Platform* - WHPX) habilitado |
| **Armazenamento** | **Unidade de Estado Sólido (SSD)** reconhecida pelo Windows para execução e pacotes das VMs |
| **Memória RAM** | Recomendado 16 GB ou mais no host (8 GB alocados por VM padrão) |
| **Conexão de Rede** | Conexão com a internet para download inicial de imagens de instalação e bootloader |
| **SDK para Compilação** | SDK do .NET 8 ou superior instalado |

> [!IMPORTANT]
> **Verificação de SSD Obrigatória**: O NovaMac consulta o volume de armazenamento via `DeviceIoControl` e bloqueia a criação de VMs em discos magnéticos rotacionais (HDD), garantindo a estabilidade e a velocidade necessárias para o sistema de arquivos APFS.

---

## Compilação e Execução

Clone o repositório e compile o executável x64 a partir do PowerShell na raiz do projeto:

```powershell
dotnet build .\NovaMac\NovaMac.csproj -p:Platform=x64
```

O aplicativo gera uma versão desempacotada (*unpackaged*) em:
`NovaMac\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\NovaMac.exe`

> [!NOTE]
> O NovaMac já inclui uma versão portátil do QEMU na pasta `EmbeddedQemu`. Caso prefira utilizar uma instalação externa do QEMU, você pode definir caminhos customizados na aba **Configurações**.

---

## Guia de Instalação do macOS

### Passo 1: Criar a Máquina Virtual
1. Abra o **Editor de VMs** na barra lateral.
2. Selecione a versão desejada do macOS no catálogo visual.
3. Escolha o canal de distribuição (**Estável Oficial**, **Developer Seed / Beta** ou **Instalador Oficial Apple**) ou selecione uma **Imagem Local** (`.dmg`, `.iso`, `.img`).
4. Ajuste os núcleos de CPU (vCPUs), memória RAM (GB), tamanho do disco virtual e resolução UEFI.
5. Clique em **Baixar macOS & Criar VM**. O aplicativo baixará a recuperação da Apple, provisionará o OpenCore e criará o disco QCOW2.

### Passo 2: Formatação do Disco no macOS Recovery
1. Na **Biblioteca**, clique em **Iniciar** no cartão da máquina criada.
2. Na tela de boot do OpenCore, selecione a opção de **macOS Recovery**.
3. Quando a recuperação carregar, abra o **Utilitário de Disco** (*Disk Utility*).
4. No menu superior, clique em **Visualizar** > **Mostrar Todos os Dispositivos**.
5. Selecione o disco virtual QEMU (geralmente identificado como `QEMU HARDDISK` ou tamanho correspondente) e clique em **Apagar** (*Erase*):
   - **Nome**: `Macintosh HD`
   - **Formato**: `APFS`
   - **Esquema**: `Mapa de Partição GUID` (*GUID Partition Map*)
6. Feche o Utilitário de Disco.

### Passo 3: Instalação do Sistema Operacional
1. Selecione **Reinstalar macOS** (*Reinstall macOS*) e avance.
2. Aceite os termos de licença e escolha o disco `Macintosh HD` recém-formatado.
3. Aguarde o download e a cópia dos arquivos. A máquina virtual reiniciará automaticamente.
4. **Pós-Reinício**: No menu do OpenCore, selecione a opção **macOS Installer**.
5. Após o término da instalação e a reinicialização final, inicialize a partir do volume **Macintosh HD**.
6. Conclua o assistente de configuração inicial do macOS.
7. *(Opcional)* No NovaMac, abra as configurações da VM no **Editor de VMs** e desative a opção **Mídia de Recuperação** para desconectar a imagem base do boot diário.

---

## Estrutura da Interface

```
NovaMac
├── Barra de Título (TitleBar)      -> Identificação sleek com status integrado do WHPX e QEMU
├── Navegação Lateral (MainNav)
│   ├── Biblioteca                 -> Gerenciamento de instâncias com cartões limpos e ações
│   ├── Editor de VMs              -> Assistente de criação e edição com 4 seções organizadas
│   ├── Diagnóstico                -> Dashboard de integridade do host (QEMU, WHPX, CPU)
│   ├── Alternar Tema              -> Troca rápida de tema Claro / Escuro
│   └── Configurações              -> Preferências de aparência, caminhos, hardware e rede
```

### Recursos da Biblioteca
- **Barra de Pesquisa e Filtros**: Localização instantânea por nome, versão do macOS, modelo SMBIOS, número de série ou porta SSH.
- **Cartões de Máquinas Virtuais Clean**:
  - Emblema visual com a versão do macOS e indicador de estado em tempo real (🟢 Rodando com PID ou ⚪ Parada).
  - Etiquetas compactas de hardware (vCPUs, RAM, capacidade do disco, resolução) e conectividade (`SSH :2222`, `VNC :5900`).
  - Botão principal contextual: **Iniciar** (quando desligada) ou **Parar** (quando ligada).
  - Botão de acesso direto para **Configurar**.
  - Menu de ações secundárias (**Mais Opções** `...`): Clonar VM, Abrir Pasta no Explorer, Copiar Comando QEMU, Visualizar Log do QEMU, Resumo Técnico e Excluir.

---

## Rede, Acesso Remoto e Compartilhamento

O NovaMac configura o QEMU com rede paravirtualizada (`vmxnet3` ou `e1000-82545em`) operando em modo NAT de usuário (`-netdev user`), isolando a VM e mapeando portas para o host Windows.

### Acesso via SSH
Ative a opção **Sessão Remota** (*Remote Login*) em *Preferências do Sistema > Compartilhamento* no macOS:
```bash
ssh -p 2222 seu_usuario@localhost
```

### Compartilhamento de Tela (VNC / Screen Sharing)
Ative o **Compartilhamento de Tela** (*Screen Sharing*) no macOS. Conecte-se utilizando qualquer cliente VNC padrão (ex: RealVNC, TightVNC, TigerVNC):
```text
Endereço: localhost:5900
```
*(Se você possuir múltiplas VMs, as portas serão automaticamente incrementadas, como 2223/5901, 2224/5902, etc.)*

### Pasta Compartilhada do Host Windows
No **Editor de VMs**, defina o campo **Pasta Compartilhada Windows**. O QEMU montará a pasta do host como uma unidade virtual acessível no macOS.

---

## Armazenamento e Pacotes de VM

Todas as instâncias são salvas como pacotes autocontidos com a extensão `.novavm`:

| Caminho | Conteúdo |
| :--- | :--- |
| `%LOCALAPPDATA%\NovaMac\settings.json` | Configurações globais e preferências do usuário |
| `%LOCALAPPDATA%\NovaMac\VMs\{Nome_ID}.novavm\` | Pasta da máquina virtual contendo discos e metadados |
| `├── config.json` | Parâmetros de hardware, portas e SMBIOS da VM |
| `├── SystemDisk.qcow2` | Disco principal dinâmico do sistema macOS (QCOW2 com cluster de 1MB) |
| `├── OpenCore.qcow2` | Bootloader OpenCore configurado |
| `├── OVMF_CODE.fd` / `OVMF_VARS.fd` | Imagens de firmware UEFI e variáveis NVRAM de 4MB |
| `├── BaseSystem.img` | Imagem da recuperação macOS |
| `├── start-vm.bat` | Script em lote autônomo para iniciar a VM diretamente via linha de comando |
| `└── qemu.log` | Registro de diagnóstico gerado caso ocorra falha de inicialização |
| `%LOCALAPPDATA%\NovaMac\Cache\` | Cache compartilhado de imagens reutilizáveis da Apple e firmware |

---

## Arquitetura do Código e Otimizações

A base de código foi refatorada para oferecer alto desempenho, manutenibilidade e baixo acoplamento:

- **`MainWindow.xaml` e `MainWindow.xaml.cs`**: Gerenciamento da janela WinUI 3, integração com a barra de título do sistema e aplicação de materiais translúcidos (Mica, Mica Alt e Acrílico).
- **`MainPage.xaml` e `MainPage.xaml.cs`**: Implementação das visualizações da interface, navegação lateral e filtros de busca otimizados com comparações ordinais em passo único.
- **`Services/QemuService.cs`**:
  - Gerenciamento de processos do QEMU com detecção de falhas e captura de logs.
  - Sistema de **cache inteligente de detecção de binários** (`FindQemuBinaries`), evitando varreduras repetidas em disco e no PATH.
  - Montagem de argumentos QEMU com parâmetros essenciais de RTC em UTC (`-rtc base=utc,clock=host`) para evitar erros de certificados SSL no instalador da Apple.
- **`Services/VmStorageService.cs`**: Persistência de arquivos de configuração JSON, rotinas de clonagem de máquinas e validação de portas disponíveis.
- **`Services/InstallationStorage.cs`**: Validação de hardware físico via chamadas de sistema Windows (`DeviceIoControl` / Seek Penalty) para garantir execução em SSDs.
- **`Services/AppleRecoveryService.cs` & `AppleSoftwareCatalogService.cs`**: Leitura e parsing assíncrono com stream delimitado de catálogos XML da Apple (SUCatalog) e download resiliente com suporte a mirrors.
- **`Services/OpenCoreService.cs`**: Provisionamento de imagens UEFI/OVMF e geração dinâmica de plists de NVRAM com boot-args personalizados.

---

## Solução de Problemas (FAQ)

### 1. O QEMU fecha imediatamente ao iniciar a VM
Abra a pasta do pacote da VM pelo menu **Mais Opções** > **Abrir Pasta do Pacote** e verifique o arquivo `qemu.log`. As causas mais comuns são:
- WHPX não habilitado no Windows.
- Opção de virtualização (Intel VT-x ou AMD-V / SVM) desativada no BIOS/UEFI do computador.
- Falta de memória RAM livre no Windows para alocar a quantidade solicitada pela VM.

### 2. O instalador do macOS exibe "Ocorreu um erro ao preparar a instalação"
Esse erro geralmente ocorre por inconsistência de relógio/data:
- O NovaMac já inclui a flag `-rtc base=utc,clock=host` para sincronizar o relógio do hardware com a Apple.
- Certifique-se de que o host Windows esteja com a hora correta e conectividade com a internet ativa.

### 3. A criação de VM é bloqueada informando que o disco não é SSD
O NovaMac exige que a unidade onde o programa está instalado seja um SSD. Caso o aplicativo esteja sendo executado em um disco rígido tradicional ou em um pendrive lento, mova a pasta do NovaMac para uma unidade SSD interna (NVMe ou SATA).

### 4. Como habilitar o WHPX manualmente pelo PowerShell?
Abra o PowerShell como Administrador e execute:
```powershell
Enable-WindowsOptionalFeature -Online -FeatureName HypervisorPlatform -All
```
Após a conclusão, reinicie o computador.

---

## Licença

Desenvolvido para fins educacionais e de pesquisa em virtualização no Windows. O software macOS é propriedade exclusiva da Apple Inc. e seu uso é regido pelo Contrato de Licença de Software (SLA) da Apple.
