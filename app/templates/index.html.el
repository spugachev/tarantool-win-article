<!DOCTYPE html>
<html lang="en">
    <head>
      <title>Users Dahsboard</title>

	<link rel="stylesheet" 	href="https://maxcdn.bootstrapcdn.com/bootstrap/3.3.7/css/bootstrap.min.css">
    </head>
    <body>
        <div class="container">
	        <div class="page-header">
	        	<h1>Users Dasbboard</h1>
	      	</div>
        	<table class="table" id="tblUsers">
        	<thead>
        		<tr>
	        		<th>#</th>
	        		<th>Id</th>
	        		<th>User</th>
	        		<th>Rating</th>
	        	</tr>
	        </thead>
	        </table>
	    </div>
	    <script>
		fetch('/users').then(resp => {
			resp.json().then(data => {

				function createCell(tr, txt){
					tr.insertCell().appendChild(
					document.createTextNode(txt));
		    		}

				let tblUsers = document.getElementById('tblUsers');
		
				for(let i=0;i<data.users.length;i++){
					var tr = tblUsers.insertRow();
					createCell(tr, i + 1);
					createCell(tr, data.users[i].id);
					createCell(tr, data.users[i].user);
					createCell(tr, data.users[i].rating); 						 						
				}
			});
		});  
		</script>
    </body>
</html>
