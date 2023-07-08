Feature: Subscription Handling
	As an administrator
	I want to register a postgres subscription
	So that I can get reliable notifications of updates

Scenario: Receive notifications
	Given a postgres server for NEventStore
	And a commit publication
	And a postgres server subscription client
	When a row is inserted into a table
	Then a notification is received
