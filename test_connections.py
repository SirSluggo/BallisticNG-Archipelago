import unittest
from panel_client import connection_url,connection_diagnostic

class ConnectionTests(unittest.TestCase):
 def test_hosted_defaults_secure(self):
  self.assertEqual(connection_url('archipelago.gg','49929'),'wss://archipelago.gg:49929')
 def test_local_defaults(self):
  for host in ['localhost','127.0.0.1','192.168.1.20','[::1]','game-server','server.local']:
   self.assertTrue(connection_url(host,'38281').startswith('ws://'),host)
 def test_explicit_schemes_preserved(self):
  self.assertEqual(connection_url('ws://archipelago.gg','49929'),'ws://archipelago.gg:49929')
  self.assertEqual(connection_url('wss://localhost','38281'),'wss://localhost:38281')
 def test_reject_private_fields_and_bad_ports(self):
  for host in ['wss://user:secret@archipelago.gg','archipelago.gg/path','archipelago.gg?password=secret','archipelago.gg:49929','https://archipelago.gg']:
   with self.assertRaises(ValueError):connection_url(host,'49929')
  for port in ['0','65536','abc','-1']:
   with self.assertRaises(ValueError):connection_url('archipelago.gg',port)
 def test_diagnostics_omit_inputs_and_exception_text(self):
  from websockets.exceptions import InvalidMessage
  message=connection_diagnostic('ws://archipelago.gg:49929',InvalidMessage('password=secret private packet'))
  self.assertIn('wss://',message)
  self.assertNotIn('secret',message)
  self.assertNotIn('archipelago.gg',message)
  self.assertNotIn('49929',message)
