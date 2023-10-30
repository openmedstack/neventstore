Feature: Grpc Event Persistence

    Background:
        Given I have a new event store server
        And an GRPC client

    Scenario: Commit event to event store
        When I commit an event to the event store
        Then the event is persisted

    Scenario: Load event stream from event store
        When I commit an event to the event store
        Then I can load the event stream from the event store
        And the event stream is returned
