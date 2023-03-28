# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.3.0] - 2023-03-28
### Added
- Get Energy Accounts v2 API

### Fixed
- x-min-v and x-v  check in API version selector


## [1.2.0] - 2023-03-21
### Added
- The Mock Data Holder Energy now utilises the [Authorisation Server](https://github.com/ConsumerDataRight/authorisation-server) as the Identity Provider
- Get Metrics API

### Changed 
- Updated to be compliant with FAPI 1.0 phase 3
- Removed Identity Server 4 project
- Removed Get Data Recipients Azure Function

## [1.1.1] - 2022-10-19
### Fixed
- Fix for Content-Type check in JwtInputFormatter used in DCR

## [1.1.0] - 2022-10-05
### Added
- Logging middleware to create a centralised list of all API requests and responses

### Fixed
- Updated supported response modes in OIDC discovery endpoint. [Issue 46](https://github.com/ConsumerDataRight/mock-data-holder/issues/46)

## [1.0.1] - 2022-08-30
### Changed
- Updated package references.

## [1.0.0] - 2022-07-22
### Added
- Azure function to perform Data Recipient discovery by polling the Get Data Recipients API of the Register.

### Changed
- First version of the Mock Data Holder Energy deployed into the CDR Sandbox.
- Updated Get Energy Concessions schema to match Consumer Data Standards 1.17.0.

## [0.1.1] - 2022-06-09
### Changed
- Person information in seed data.
- Build and Test action to download containers from docker hub.

### Fixed
- Intermittent issue when creating the LogEventsManageAPI database table.

## [0.1.0] - 2022-05-25

### Added
- First release of the Mock Data Holder Energy.
