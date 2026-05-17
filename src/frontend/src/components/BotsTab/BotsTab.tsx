import { Show } from 'solid-js';
import { BotList } from './BotList';
import { AgentList } from './AgentList';
import { CreateBotModal } from './CreateBotModal';
import { ConfigureBotModal } from './ConfigureBotModal';
import { useBotsTab } from './useBotsTab';
import Flexbox from '../ui/Flexbox';
import styles from './BotsTab.module.css';

export default function BotsTab() {
  const s = useBotsTab();

  return (
    <Flexbox direction="vertical" gap={1.5} data-testid="bots-tab" class={styles.container}>
      {/* Load error */}
      <Show when={s.loadError()}>
        <div role="alert" class={styles.alertError}>
          {s.loadError()}
        </div>
      </Show>

      {/* Action error (start/stop/delete/revoke) */}
      <Show when={s.actionError()}>
        <div role="alert" class={styles.alertError}>
          {s.actionError()}
        </div>
      </Show>

      {/* ---- Section 1: Your Bots ---- */}
      <BotList
        bots={s.bots()}
        agents={s.agents()}
        isLoading={s.isLoadingBots()}
        actionBotId={s.actionBotId()}
        assignAgentBotId={s.assignAgentBotId()}
        assignAgentId={s.assignAgentId()}
        isAssigning={s.isAssigning()}
        assignError={s.assignError()}
        onCreate={() => s.setShowCreateModal(true)}
        onSetAssignAgentId={(id) => s.setAssignAgentId(id)}
        onOpenAssignAgent={s.handleOpenAssignAgent}
        onCancelAssignAgent={() => s.setAssignAgentBotId(null)}
        onConfirmAssignAgent={s.handleAssignAgent}
        onConfigure={s.openConfigureModal}
        onStart={s.handleStartBot}
        onStop={s.handleStopBot}
        onDelete={s.handleDeleteBot}
        onRevokeToken={s.handleRevokeToken}
      />

      {/* ---- Section 2: Available Agents ---- */}
      <AgentList
        agents={s.agents()}
        isLoading={s.isLoadingAgents()}
        unassignedBots={s.unassignedBots()}
        assignAgentBotId={s.assignAgentBotId()}
        isAssigning={s.isAssigning()}
        assignError={s.assignError()}
        onOpenAssignToBot={s.handleOpenAssignToBot}
        onSetAssignAgentBotIdRaw={(v) => s.setAssignAgentBotId(v)}
        onCancelAssignToBot={() => s.setAssignAgentBotId(null)}
        onConfirmAssignToBot={s.handleConfirmAssignToBot}
      />

      {/* ---- Create Bot Modal ---- */}
      <Show when={s.showCreateModal()}>
        <CreateBotModal
          agents={s.agents()}
          username={s.createUsername()}
          displayName={s.createDisplayName()}
          tokenName={s.createTokenName()}
          agentId={s.createAgentId()}
          paramValues={s.createParamValues()}
          isCreating={s.isCreating()}
          createError={s.createError()}
          newToken={s.newToken()}
          copiedToken={s.copiedToken()}
          onSetUsername={s.setCreateUsername}
          onSetDisplayName={s.setCreateDisplayName}
          onSetTokenName={s.setCreateTokenName}
          onAgentChange={s.handleCreateAgentChange}
          onSetParamValues={(updater) => s.setCreateParamValues((prev) => updater(prev))}
          onSubmit={s.handleCreateBot}
          onDismiss={s.handleDismissCreateModal}
          onCopyToken={s.handleCopyToken}
        />
      </Show>

      {/* ---- Configure Modal ---- */}
      <Show when={s.configuringBot()}>
        {(bot) => (
          <ConfigureBotModal
            bot={bot()}
            agent={s.configuredBotAgent()}
            paramValues={s.configParamValues()}
            isSaving={s.isSavingConfig()}
            configError={s.configError()}
            configSuccess={s.configSuccess()}
            onSetParamValues={(updater) => s.setConfigParamValues((prev) => updater(prev))}
            onSave={s.handleSaveConfig}
            onClose={() => s.setConfiguringBot(null)}
          />
        )}
      </Show>
    </Flexbox>
  );
}
