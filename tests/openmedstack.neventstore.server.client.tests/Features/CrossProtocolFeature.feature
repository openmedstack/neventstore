Feature: Cross Protocol Feature
	Simple calculator for adding two numbers

	Background:
		Given I have a new event store server
		And both an GRPC and HTTP client

	Scenario: Commit event to event store using HTTP
		When I commit an event to the event store using the HTTP client
		Then the event is persisted

	Scenario: Commit event to event store using GRPC
		When I commit an event to the event store using the GRPC client
		Then the event is persisted

	Scenario: Load event stream from event store from HTTP to GRPC
		When I commit an event to the event store using the HTTP client
		Then I can load the event stream from the event store using the GRPC client
		And the event stream is returned

	Scenario: Load event stream from event store from GRPC to HTTP
		When I commit an event to the event store using the GRPC client
		Then I can load the event stream from the event store using the HTTP client
		And the event stream is returned

	Scenario: Deleting  GRPC event stream from event store using HTTP
		When I commit an event to the event store using the GRPC client
		And then delete it using the HTTP client
		Then I can load the event stream from the event store using the HTTP client
		And an empty event stream is returned

	Scenario: Deleting  HTTP event stream from event store using GRPC
		When I commit an event to the event store using the HTTP client
		And then delete it using the GRPC client
		Then I can load the event stream from the event store using the GRPC client
		And an empty event stream is returned
