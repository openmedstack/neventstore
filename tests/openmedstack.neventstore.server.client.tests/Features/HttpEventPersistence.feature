Feature: Http Event Persistence

    Background:
        Given I have a new event store server
        And an HTTP client

    Scenario: Commit event to event store
        When I commit an event to the event store
        Then the event is persisted

    Scenario: Load event stream from event store
        When I commit an event to the event store
        Then I can load the event stream from the event store
        And the event stream is returned

    Scenario: Deleting event stream from event store
        When I commit an event to the event store
        And then delete it
        Then I can load the event stream from the event store
        And an empty event stream is returned
