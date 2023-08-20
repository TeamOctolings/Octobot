# Contributing Guidelines

Thank you for showing interest in the development of Boyfriend. We aim to provide a good collaborating environment for
everyone involved, and as such have decided to list some of the most important things to keep in mind in the process.
Before starting, please read our [Code of Conduct](CODE_OF_CONDUCT.md)

## Reporting bugs

A **bug** is a situation in which there is something clearly wrong with the bot. Examples of applicable bug reports are:

- The bot doesn't reply to a command
- The bot sends the same message twice
- The bot takes a long time to a respond if I use this specific command
- An embed the bot sent has incorrect information in it

To track bug reports, we primarily use GitHub **issues**. When opening an issue, please keep in mind the following:

- Before opening the issue, please search for any similar existing issues using the text search bar and the issue
  labels. This includes both open and closed issues (we may have already fixed something, but the fix hasn't yet been
  released).
- When opening the issue, please fill out as much of the issue template as you can. In particular, please make sure to
  include console output and screenshots as much as possible.
- We may ask you for follow-up information to reproduce or debug the problem. Please look out for this and provide
  follow-up info if we request it.

## Submitting pull requests

While pull requests from unaffiliated contributors are welcome, please note that the core team *may* be focused on
internal issues that haven't been published to the issue tracker yet. Reviewing PRs is done on a best-effort basis, so
please be aware that it may take a while before a core maintainer gets around to review your change.

The [issue tracker](https://github.com/LabsDevelopment/Boyfriend/issues) should provide plenty of issues to start with.
Make sure to check that an issue you're planning to resolve does not already have people working on it and that there
are no PRs associated with it

In the case of simple issues, a direct PR is okay. However, if you decide to work on an existing issue which doesn't
seem trivial, **please ask us first**. This way we can try to estimate if it is a good fit for you and provide the
correct direction on how to address it.

If you'd like to propose a subjective change to one of the UI/UX aspects of the bot, or there is a bigger task you'd
like to work on, but there is no corresponding issue yet for it, **please open an issue first** to avoid wasted effort.

Aside from the above, below is a brief checklist of things to watch out when you're preparing your code changes:

- Make sure you're comfortable with the principles of object-oriented programming, the syntax of C\# and your
  development environment.
- Make sure you are familiar with [git](https://git-scm.com/)
  and [the pull request workflow](https://help.github.com/en/github/collaborating-with-issues-and-pull-requests/proposing-changes-to-your-work-with-pull-requests).
- Please do not make code changes via the GitHub web interface.
- Please make sure your development environment respects the .editorconfig file present in the repository. Our code
  style differs from most C\# projects and is closer to something you see in Java projects.
- Please test your changes. We expect most new features and bugfixes to be tested in an environment similar to
  production.

After you're done with your changes and you wish to open the PR, please observe the following recommendations:

- Please submit the pull request from
  a [topic branch](https://git-scm.com/book/en/v2/Git-Branching-Branching-Workflows#_topic_branch) (not `master`), and
  keep the *Allow edits from maintainers* check box selected, so that we can push fixes to your PR if necessary.
- Please avoid pushing untested or incomplete code.
- Please do not force-push or rebase unless we ask you to.
- Please do not merge `master` continually if there are no conflicts to resolve. We will do this for you when the change
  is ready for merge.

We are highly committed to quality when it comes to Boyfriend. This means that contributions from less experienced
community members can take multiple rounds of review to get to a mergeable state. We try our utmost best to never
conflate a person with the code they authored, and to keep the discussion focused on the code at all times. Please
consider our comments and requests a learning experience.
