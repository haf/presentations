node default {
  group { "puppet":
    ensure => "present",
  }

  Exec { path => '/bin:/usr/bin:/usr/sbin', }
  File { owner => 0, group => 0, mode => 0644, }

  class { 'epel': }
  class { 'rabbitmq':
    plugins => true,
    require => Class['epel']
  }
}
