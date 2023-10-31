Feature: Persistence Engine Behavior
As a developer
I want to ensure that the persistence engine behaves as expected

    Scenario Outline: Process head moved
        Given a <type> persistence engine
        And the persistence is initialized
        And a persisted stream with a single event
        When committing after another thread or process has moved the stream head
        Then should update the stream revision accordingly

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Header has a name that contains a period
        Given a <type> persistence engine
        And the persistence is initialized
        And a persisted stream with a header containing a period
        When getting commit
        Then should return the header with the period

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: When a commit is successfully persisted
        Given a <type> persistence engine
        And the persistence is initialized
        And a persisted event stream
        When getting commit
        Then should correctly persist the stream identifier
        And should correctly persist the stream revision
        And should correctly persist the commit sequence
        And should correctly persist the commit stamp
        And should correctly persist the headers
        And should correctly persist the events
        And should cause the stream to be found in the list of streams to snapshot

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Reading a specific revision
        Given a <type> persistence engine
        And the persistence is initialized
        And an event stream with 3 commits
        When getting a range from 3 to 5
        Then should start from the commit which contains the minimum stream revision specified
        And should read up to the commit which contains the maximum stream revision specified

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Reading from a given revision to commit revision
        Given a <type> persistence engine
        And the persistence is initialized
        And an event stream with 3 commits
        When getting a range from 3 to 6
        Then should start from the commit which contains the minimum stream revision specified
        And should read up to the commit which contains the maximum stream revision specified

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Committing a stream with the same sequence id
    Test to ensure the uniqueness of BucketId+StreamId+CommitSequence to avoid concurrency issues
        Given a <type> persistence engine
        And the persistence is initialized
        And a persisted stream
        When committing a stream with the same sequence id
        Then should update to include competing events

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Attempting to persist a commit twice
        Given a <type> persistence engine
        And the persistence is initialized
        And an already persisted stream
        When committing the stream again
        Then should throw a duplicate commit exception

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Attempting to persist a commit id twice on same stream
        Given a <type> persistence engine
        And the persistence is initialized
        And an existing commit attempt
        When committing again on the same stream
        Then should throw a duplicate commit exception

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Committing more events than the configured page size
        Given a <type> persistence engine
        And the persistence is initialized
        And more commits than the page size are persisted
        When loading all committed events
        Then should load the same number of commits which have been persisted
        And should have the same commits as those which have been persisted

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Getting from checkpoint amount of commits exceeds page size
        Given a <type> persistence engine
        And the persistence is initialized
        And more streams persisted than the page size
        When getting all commits
        Then should have expected number of commits

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Saving a snapshot
        Given a <type> persistence engine
        And the persistence is initialized
        And a non-snapshotted event stream
        When saving a snapshot
        Then should save the snapshot
        And should be able to load the snapshot

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Retrieving a snapshot
        Given a <type> persistence engine
        And the persistence is initialized
        And an event stream with snapshots
        When loading a snapshot to far ahead
        Then should return the most recent prior snapshot
        And should have the correct payload
        And should have the correct stream id

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: A snapshot has been added to the most recent commit of a stream
        Given a <type> persistence engine
        And the persistence is initialized
        And multiple committed streams
        When adding a snapshot to the most recent commit
        Then should no longer find the stream in the set of streams to be snapshotted

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Adding commit after snapshot
        Given a <type> persistence engine
        And the persistence is initialized
        And a snapshotted event stream
        When adding commit after the snapshot
        Then should find the stream in the set of streams to be snapshotted when within the snapshot threshold
        And should not find the stream in the set of streams to be snapshotted when outside the snapshot threshold

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Reading all commits from a particular point in time
        Given a <type> persistence engine
        And the persistence is initialized
        Given some streams created now
        When getting streams from now onwards
        Then should return the streams created on or after that point in time

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Paging over all commits from a particular point in time
        Given a <type> persistence engine
        And the persistence is initialized
        Given more streams than the page size committed now
        When loading streams from now onwards
        Then should load the same number of commits which have been persisted
        And should load the same commits which have been persisted

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Paging over all commits from a particular checkpoint
        Given a <type> persistence engine
        And the persistence is initialized
        And one more committed stream than page size
        When loading streams from a checkpoint
        Then should load the same number of commits which have been persisted starting from the checkpoint
        And should load only the commits starting from the checkpoint

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Paging over all commits of a bucket from a particular checkpoint
        Given a <type> persistence engine
        And the persistence is initialized
        And multiple streams in different buckets
        When getting commits from bucket 1 from a checkpoint
        Then should load the same number of commits from bucket 1 which have been persisted starting from the checkpoint
        And should load only commits from bucket 1 from the checkpoint
        And should not load the commits from bucket 2

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Committing a stream with the same id as a stream in another bucket
        Given a <type> persistence engine
        And the persistence is initialized
        And a stream committed in bucket A
        And a stream to commit to bucket B
        When committing to bucket B
        Then should succeed
        And should persist to the correct bucket
        And should not affect the stream from the other bucket

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Saving a snapshot for a stream with the same id as a stream in another bucket
        Given a <type> persistence engine
        And the persistence is initialized
        And 2 persisted streams in different buckets
        When saving a snapshot for the stream in bucket B
        Then should succeed
        And not affect snapshots in bucket A

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Reading all commits from a particular point in time and there are streams in multiple buckets
        Given a <type> persistence engine
        And the persistence is initialized
        And streams committed to multiple buckets
        When getting commits from bucket A
        Then should not return commits from other buckets

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Getting all commits since checkpoint and there are streams in multiple buckets
        Given a <type> persistence engine
        And the persistence is initialized
        And streams committed to buckets A and B
        When getting all commits from bucket A
        Then has commits from bucket A
        And is returned in order of checkpoint

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Reading all commits from start
        Given a <type> persistence engine
        And the persistence is initialized
        When reading all commits from start
        Then should not throw and exception

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Purging all streams and commits
        Given a <type> persistence engine
        And the persistence is initialized
        And a persisted event stream
        When purging all streams and commits
        Then should not find any commits stored
        And should not find any streams to snapshot

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Purging all streams and commits from all buckets
        Given a <type> persistence engine
        And the persistence is initialized
        And event streams persisted in different buckets
        When purging all streams and commits
        Then should purge all commits stored in bucket A
        And should purge all commits stored in bucket B
        And should purge all streams to snapshot in bucket A
        And should purge all streams to snapshot in bucket B

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Invoking after disposing
        Given a <type> persistence engine
        And the persistence is initialized
        When the storage is disposed
        And making a commit
        Then should throw a disposed exception

        Examples:
          | type      |
          | in-memory |

    Scenario Outline: Large payload
        Given a <type> persistence engine
        And the persistence is initialized
        When committing stream with a large payload
        Then reads the whole body

        Examples:
          | type      |
          | in-memory |
