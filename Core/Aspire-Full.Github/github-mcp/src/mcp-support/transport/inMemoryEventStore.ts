/**
 * Derived from the MCP SDK streamable HTTP example; keeps resumability logic under source control.
 */
export interface EventReplayContext {
  send(eventId: string, message: unknown): Promise<void> | void;
}

interface StoredEvent {
  streamId: string;
  message: unknown;
}

export class InMemoryEventStore {
  private readonly events = new Map<string, StoredEvent>();

  private generateEventId(streamId: string): string {
    return `${streamId}_${Date.now()}_${Math.random().toString(36).slice(2, 10)}`;
  }

  private getStreamIdFromEventId(eventId: string): string {
    const [id] = eventId.split("_");
    return id ?? "";
  }

  async storeEvent(streamId: string, message: unknown): Promise<string> {
    const eventId = this.generateEventId(streamId);
    this.events.set(eventId, { streamId, message });
    return eventId;
  }

  async replayEventsAfter(lastEventId: string | undefined, context: EventReplayContext): Promise<string> {
    if (!lastEventId || !this.events.has(lastEventId)) {
      return "";
    }

    const streamId = this.getStreamIdFromEventId(lastEventId);
    if (!streamId) {
      return "";
    }

    const sortedEvents = [...this.events.entries()].sort((a, b) => a[0].localeCompare(b[0]));
    let seenLast = false;

    for (const [eventId, stored] of sortedEvents) {
      if (stored.streamId !== streamId) {
        continue;
      }

      if (eventId === lastEventId) {
        seenLast = true;
        continue;
      }

      if (seenLast) {
        await context.send(eventId, stored.message);
      }
    }

    return streamId;
  }
}
